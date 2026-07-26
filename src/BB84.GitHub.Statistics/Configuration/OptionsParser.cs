using System.Collections;
using System.Globalization;

namespace BB84.GitHub.Statistics.Configuration;

internal enum ParseOutcome
{
	Success,
	HelpRequested,
	Error,
}

internal readonly record struct ParseResult(ParseOutcome Outcome, AppOptions Options);

/// <summary>
/// Hand-written parser layering command line over environment over defaults.
/// </summary>
/// <remarks>
/// <para>
/// The Zig original derived flags, environment names, defaults and <c>--help</c>
/// from one struct via comptime reflection. Reflection-based binding is not
/// NativeAOT-safe, so the equivalent here is an explicit option table with
/// setter delegates. The three-phase "first writer wins" precedence
/// (command line, then environment, then default) is preserved exactly.
/// </para>
/// <para>
/// Deviations from the Zig parser, both deliberate: a repeated flag is
/// last-wins rather than reported as an unknown argument, and integer values
/// are parsed as decimal only rather than with base-prefix detection.
/// </para>
/// </remarks>
internal static class OptionsParser
{
	private sealed record OptionDef(
			string Name,
			string Description,
			Action<AppOptions, string>? SetValue = null,
			Action<AppOptions, bool>? SetFlag = null)
	{
		public bool IsFlag => SetFlag is not null;

		public string Flag => "--" + Name.Replace('_', '-');
	}

	private static readonly OptionDef[] Options =
	[
			new("access_token", "GitHub personal access token (classic) with read:user, user:email, repo.",
						SetValue: static (o, v) => o.AccessToken = v),
				new("json_input_file", "Read statistics from this JSON file instead of the GitHub API ('-' for stdin).",
						SetValue: static (o, v) => o.JsonInputFile = v),
				new("json_output_file", "Dump collected statistics to this JSON file ('-' for stdout).",
						SetValue: static (o, v) => o.JsonOutputFile = v),
				new("silent", "Log errors only.",
						SetFlag: static (o, v) => o.Silent = v),
				new("debug", "Log everything.",
						SetFlag: static (o, v) => o.Debug = v),
				new("verbose", "Log informational messages.",
						SetFlag: static (o, v) => o.Verbose = v),
				new("exclude_repos", "Repositories to exclude, separated by commas. Supports * globs.",
						SetValue: static (o, v) => o.ExcludeRepos = v),
				new("exclude_langs", "Languages to exclude, separated by commas. Supports * globs.",
						SetValue: static (o, v) => o.ExcludeLangs = v),
				new("exclude_private", "Exclude private repositories from the aggregate statistics.",
						SetFlag: static (o, v) => o.ExcludePrivate = v),
				new("overview_output_file", "Where to write the overview SVG (default overview.svg).",
						SetValue: static (o, v) => o.OverviewOutputFile = v),
				new("languages_output_file", "Where to write the languages SVG (default languages.svg).",
						SetValue: static (o, v) => o.LanguagesOutputFile = v),
				new("overview_template", "Use this file instead of the built-in overview template.",
						SetValue: static (o, v) => o.OverviewTemplate = v),
				new("languages_template", "Use this file instead of the built-in languages template.",
						SetValue: static (o, v) => o.LanguagesTemplate = v),
				new("overview_fields", "Overview rows to render, in order, separated by commas. Omit for the default set.",
						SetValue: static (o, v) => o.OverviewFields = v),
				new("max_retries", "Retries against the contributor statistics API before cloning instead (default 25).",
						SetValue: static (o, v) => o.MaxRetries = ParseInt(v)),
				new("version", "Print the version and exit.",
						SetFlag: static (o, v) => o.Version = v),
				new("dump_overview_template", "Write the built-in overview template to this path and exit.",
						SetValue: static (o, v) => o.DumpOverviewTemplate = v),
				new("dump_languages_template", "Write the built-in languages template to this path and exit.",
						SetValue: static (o, v) => o.DumpLanguagesTemplate = v),
		];

	public static ParseResult Parse(string[] args, TextWriter stdout, TextWriter stderr)
			=> Parse(args, Environment.GetEnvironmentVariables(), stdout, stderr);

	internal static ParseResult Parse(
			string[] args,
			IDictionary environment,
			TextWriter stdout,
			TextWriter stderr)
	{
		AppOptions options = new();
		HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

		if (!ApplyCommandLine(args, options, seen, stdout, stderr, out ParseOutcome earlyExit))
		{
			return new ParseResult(earlyExit, options);
		}

		if (!ApplyEnvironment(environment, options, seen, stderr))
		{
			return new ParseResult(ParseOutcome.Error, options);
		}

		if (!Validate(options, stdout, stderr))
		{
			return new ParseResult(ParseOutcome.Error, options);
		}

		return new ParseResult(ParseOutcome.Success, options);
	}

	private static bool ApplyCommandLine(
			string[] args,
			AppOptions options,
			HashSet<string> seen,
			TextWriter stdout,
			TextWriter stderr,
			out ParseOutcome earlyExit)
	{
		earlyExit = ParseOutcome.Success;

		for (int i = 0; i < args.Length; i++)
		{
			string raw = args[i];

			if (raw is "-h" or "--help")
			{
				PrintUsage(stdout);
				earlyExit = ParseOutcome.HelpRequested;
				return false;
			}

			if (!raw.StartsWith("--", StringComparison.Ordinal))
			{
				stderr.WriteLine($"Unknown argument: '{raw}'");
				PrintUsage(stdout);
				earlyExit = ParseOutcome.Error;
				return false;
			}

			string name = raw[2..].Replace('-', '_');
			OptionDef? option = Find(name);
			if (option is null)
			{
				stderr.WriteLine($"Unknown argument: '{raw}'");
				PrintUsage(stdout);
				earlyExit = ParseOutcome.Error;
				return false;
			}

			if (option.IsFlag)
			{
				option.SetFlag!(options, true);
			}
			else
			{
				i++;
				if (i >= args.Length)
				{
					stderr.WriteLine($"Missing required value for argument {raw}");
					PrintUsage(stdout);
					earlyExit = ParseOutcome.Error;
					return false;
				}

				try
				{
					option.SetValue!(options, args[i]);
				}
				catch (FormatException e)
				{
					stderr.WriteLine($"Invalid value for {raw}: {e.Message}");
					PrintUsage(stdout);
					earlyExit = ParseOutcome.Error;
					return false;
				}
			}

			_ = seen.Add(option.Name);
		}

		return true;
	}

	private static bool ApplyEnvironment(
			IDictionary environment,
			AppOptions options,
			HashSet<string> seen,
			TextWriter stderr)
	{
		foreach (DictionaryEntry entry in environment)
		{
			if (entry.Key is not string rawKey || entry.Value is not string value)
			{
				continue;
			}

			string name = rawKey.Replace('-', '_');
			OptionDef? option = Find(name);
			if (option is null || seen.Contains(option.Name))
			{
				continue;
			}

			if (option.IsFlag)
			{
				// Matches the Zig rule: any non-empty value except "false" is true.
				option.SetFlag!(options, value.Length > 0 && !value.Equals("false", StringComparison.OrdinalIgnoreCase));
			}
			else
			{
				try
				{
					option.SetValue!(options, value);
				}
				catch (FormatException e)
				{
					stderr.WriteLine($"Invalid value for {rawKey}: {e.Message}");
					return false;
				}
			}

			_ = seen.Add(option.Name);
		}

		return true;
	}

	private static bool Validate(AppOptions options, TextWriter stdout, TextWriter stderr)
	{
		if (string.IsNullOrEmpty(options.AccessToken) &&
				options.JsonInputFile is null &&
				!options.Version &&
				options.DumpOverviewTemplate is null &&
				options.DumpLanguagesTemplate is null)
		{
			stderr.WriteLine("You must pass an input file or a GitHub token.");
			PrintUsage(stdout);
			return false;
		}

		return true;
	}

	private static OptionDef? Find(string normalizedName)
	{
		foreach (OptionDef option in Options)
		{
			if (string.Equals(option.Name, normalizedName, StringComparison.OrdinalIgnoreCase))
			{
				return option;
			}
		}

		return null;
	}

	private static int ParseInt(string value) =>
			int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
					? parsed
					: throw new FormatException($"Expected an integer, got '{value}'.");

	public static void PrintUsage(TextWriter writer)
	{
		writer.WriteLine("Usage: github-stats [options]");
		writer.WriteLine();
		writer.WriteLine("Every option can also be set as an environment variable, e.g. --access-token");
		writer.WriteLine("as ACCESS_TOKEN. Command line arguments take precedence.");
		writer.WriteLine();
		writer.WriteLine("Options:");

		int width = 0;
		foreach (OptionDef option in Options)
		{
			width = Math.Max(width, option.Flag.Length + (option.IsFlag ? 0 : 6));
		}

		foreach (OptionDef option in Options)
		{
			string left = option.IsFlag ? option.Flag : option.Flag + " VALUE";
			writer.WriteLine($"  {left.PadRight(width)}  {option.Description}");
		}

		writer.WriteLine($"  {"-h, --help".PadRight(width)}  Show this help and exit.");
	}
}
