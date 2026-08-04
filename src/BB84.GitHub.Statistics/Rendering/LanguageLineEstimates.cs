using System.Collections.Frozen;

namespace BB84.GitHub.Statistics.Rendering;

/// <summary>
/// Converts a language's byte total into an approximate line count.
/// </summary>
/// <remarks>
/// <para>
/// GitHub reports only bytes per language: the <c>languages</c> connection in
/// <c>GraphQlQueries.CommitContributionsByRange</c> asks for <c>edges.size</c>,
/// <c>node.name</c> and <c>node.color</c>, and no endpoint anywhere in the REST
/// or GraphQL surface exposes a line count or a file count. The only alternative
/// would be cloning every repository and counting, which the tool deliberately
/// does not do outside the <see cref="Statistics.LinesChangedResolver"/> fallback.
/// </para>
/// <para>
/// So this is an estimate, not a measurement. Divide the byte total by a typical
/// line length for the language and accept the error, which is dominated by how
/// much of a repository is generated files, vendored code or long data literals.
/// Every rendered value is prefixed with <c>~</c> so it never reads as exact.
/// </para>
/// </remarks>
internal static class LanguageLineEstimates
{
	/// <summary>
	/// Used for any language not in <see cref="BytesPerLine"/>. Sits between the
	/// terse scripting languages and the verbose markup ones, so an unlisted
	/// language is wrong by a factor well under two either way.
	/// </summary>
	internal const int DefaultBytesPerLine = 32;

	/// <summary>
	/// Average bytes per line, keyed by the language names GitHub's linguist
	/// reports. Ordinal comparison: linguist names are stable and case-exact.
	/// </summary>
	private static readonly FrozenDictionary<string, int> BytesPerLine =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				// Shell and build scripts: short lines, many of them blank.
				["Shell"] = 26,
				["PowerShell"] = 26,
				["Batchfile"] = 26,
				["Makefile"] = 26,
				["Dockerfile"] = 26,

				// Dynamic languages with light syntax and no braces.
				["Python"] = 28,
				["Ruby"] = 28,
				["Lua"] = 28,
				["Perl"] = 28,
				["R"] = 28,

				// Statically typed, brace-delimited, one statement per line.
				["C"] = 31,
				["C++"] = 31,
				["C#"] = 31,
				["Java"] = 31,
				["Go"] = 31,
				["Rust"] = 31,
				["Kotlin"] = 31,
				["Swift"] = 31,
				["Objective-C"] = 31,
				["F#"] = 31,
				["Visual Basic .NET"] = 31,

				// Same shape, but longer identifiers and more chained calls.
				["JavaScript"] = 33,
				["TypeScript"] = 33,
				["PHP"] = 33,
				["Dart"] = 33,
				["Scala"] = 33,
				["Vue"] = 33,
				["Svelte"] = 33,

				// SQL dialects: verbose keywords, wide column lists.
				["SQL"] = 35,
				["TSQL"] = 35,
				["PLpgSQL"] = 35,
				["PLSQL"] = 35,

				// Markup, styling and configuration: attributes push lines out.
				["HTML"] = 42,
				["XML"] = 42,
				["CSS"] = 42,
				["SCSS"] = 42,
				["Less"] = 42,
				["JSON"] = 42,
				["YAML"] = 42,
				["TOML"] = 42,

				// Prose, where a line is a sentence or a paragraph.
				["Markdown"] = 50,
				["TeX"] = 50,
			}.ToFrozenDictionary(StringComparer.Ordinal);

	/// <summary>Approximate lines of code behind <paramref name="bytes"/>.</summary>
	public static long Estimate(string name, long bytes)
	{
		if (bytes <= 0)
		{
			return 0;
		}

		int divisor = BytesPerLine.GetValueOrDefault(name, DefaultBytesPerLine);

		return bytes / divisor;
	}
}
