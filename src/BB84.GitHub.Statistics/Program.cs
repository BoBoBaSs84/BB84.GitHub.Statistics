using BB84.GitHub.Statistics;
using BB84.GitHub.Statistics.Configuration;
using BB84.GitHub.Statistics.GitHub;
using BB84.GitHub.Statistics.Logging;
using BB84.GitHub.Statistics.Rendering;
using BB84.GitHub.Statistics.Statistics;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text;
using System.Text.Json;

ParseResult parsed = OptionsParser.Parse(args, Console.Out, Console.Error);
switch (parsed.Outcome)
{
	case ParseOutcome.HelpRequested:
		return 0;
	case ParseOutcome.Error:
		return 1;
}

AppOptions options = parsed.Options;

if (options.Version)
{
	Console.Out.Write(
			$"""
         GitHub Stats version {BuildVersion.Value}
         https://github.com/jstrieb/github-stats
         Created by Jacob Strieb

         """);
	return 0;
}

if (options.DumpOverviewTemplate is { } overviewDumpPath)
{
	WriteFile(overviewDumpPath, Templates.Overview);
	return 0;
}

if (options.DumpLanguagesTemplate is { } languagesDumpPath)
{
	WriteFile(languagesDumpPath, Templates.Languages);
	return 0;
}

using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder
		.SetMinimumLevel(ResolveLogLevel(options))
		.AddConsole(console =>
				// Everything goes to stderr so `--overview-output-file -` can stream SVG to stdout.
				console.LogToStandardErrorThreshold = LogLevel.Trace)
		.AddSimpleConsole(console =>
		{
			console.SingleLine = true;
			console.TimestampFormat = null;
		}));

ILogger programLogger = loggerFactory.CreateLogger("github-stats");

try
{
	StatisticsDocument statistics;

	if (options.JsonInputFile is { } inputPath)
	{
		Log.ReadingData(programLogger, inputPath);
		statistics = JsonSerializer.Deserialize(
				ReadFile(inputPath),
				StatisticsJsonContext.Default.StatisticsDocument)
				?? throw new InvalidDataException($"'{inputPath}' did not contain a statistics document.");
	}
	else
	{
		string token = options.AccessToken
				?? throw new InvalidOperationException("No access token available.");

		Log.CollectingStatistics(programLogger);

		using HttpClient http = GitHubApiClient.CreateHttpClient(token);
		GitHubApiClient client = new(http, loggerFactory.CreateLogger<GitHubApiClient>());
		GitCli git = new(loggerFactory.CreateLogger<GitCli>());
		LinesChangedResolver resolver = new(client, git, loggerFactory.CreateLogger<LinesChangedResolver>());
		StatisticsCollector collector = new(client, resolver, loggerFactory.CreateLogger<StatisticsCollector>());

		statistics = await collector.CollectAsync(token, options.MaxRetries, CancellationToken.None);
	}

	if (options.JsonOutputFile is { } jsonPath)
	{
		Log.WritingData(programLogger, jsonPath);
		WriteFile(jsonPath, JsonSerializer.Serialize(statistics, StatisticsJsonContext.Default.StatisticsDocument));
	}

	AggregateStats aggregate = Aggregator.Aggregate(statistics, options);

	string overviewPath = options.OverviewOutputFile ?? "overview.svg";
	Log.WritingData(programLogger, overviewPath);
	WriteFile(overviewPath, SvgTemplate.Fill(
			options.OverviewTemplate is { } overviewTemplatePath
					? ReadFile(overviewTemplatePath)
					: Templates.Overview,
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["name"] = aggregate.Name,
				["stars"] = SvgTemplate.FormatNumber(aggregate.Stars),
				["forks"] = SvgTemplate.FormatNumber(aggregate.Forks),
				["contributions"] = SvgTemplate.FormatNumber(aggregate.Contributions),
				["lines_changed"] = SvgTemplate.FormatNumber(aggregate.LinesChanged),
				["views"] = SvgTemplate.FormatNumber(aggregate.Views),
				["repos"] = SvgTemplate.FormatNumber(aggregate.Repos),
			}));

	(string progress, string langList) = LanguagesRenderer.Render(aggregate.Languages, aggregate.LanguagesTotal);

	string languagesPath = options.LanguagesOutputFile ?? "languages.svg";
	Log.WritingData(programLogger, languagesPath);
	WriteFile(languagesPath, SvgTemplate.Fill(
			options.LanguagesTemplate is { } languagesTemplatePath
					? ReadFile(languagesTemplatePath)
					: Templates.Languages,
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["lang_list"] = langList,
				["progress"] = progress,
			}));

	return 0;
}
catch (Exception e) when (e is HttpRequestException or IOException or InvalidDataException or InvalidFieldException)
{
	Log.Failed(programLogger, e.Message);
	return 1;
}

static LogLevel ResolveLogLevel(AppOptions options) => options switch
{
	{ Silent: true } => LogLevel.Error,
	{ Debug: true } => LogLevel.Debug,
	{ Verbose: true } => LogLevel.Information,
	_ => LogLevel.Warning,
};

static string ReadFile(string path) =>
		path == "-" ? Console.In.ReadToEnd() : File.ReadAllText(path);

static void WriteFile(string path, string contents)
{
	if (path == "-")
	{
		Console.Out.Write(contents);
		Console.Out.Flush();
		return;
	}

	// UTF-8 with no BOM, so generated SVGs stay byte-identical across platforms.
	File.WriteAllText(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

/// <summary>The SVG templates compiled into the binary.</summary>
internal static class Templates
{
	public static string Overview => Read("overview.svg");

	public static string Languages => Read("languages.svg");

	private static string Read(string name)
	{
		using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
				?? throw new InvalidOperationException($"Embedded template '{name}' is missing.");
		using StreamReader reader = new(stream, Encoding.UTF8);
		return reader.ReadToEnd();
	}
}
