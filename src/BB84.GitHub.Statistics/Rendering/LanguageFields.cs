using System.Globalization;

namespace BB84.GitHub.Statistics.Rendering;

/// <summary>Everything a language field may need to render one legend entry.</summary>
/// <param name="Language">The language being rendered.</param>
/// <param name="Index">Its zero-based position in the size-descending list.</param>
/// <param name="Total">Byte total across every language, for the percentage.</param>
internal readonly record struct LanguageRow(AggregatedLanguage Language, int Index, long Total);

/// <summary>One selectable span of a language legend entry.</summary>
/// <param name="Id">What the user types in <c>--languages-fields</c>.</param>
/// <param name="CssClass">
/// The <c>class</c> attribute on the emitted <c>&lt;span&gt;</c>, which is what
/// <c>languages.svg</c> styles. The language name keeps the historical
/// <c>lang</c> class rather than a class named after its id.
/// </param>
/// <param name="Value">Renders the span's text. The caller escapes the result.</param>
internal sealed record LanguageField(
		string Id,
		string CssClass,
		Func<LanguageRow, string> Value);

/// <summary>
/// The catalogue of spans a language legend entry can show, and the parser for
/// <c>--languages-fields</c>.
/// </summary>
/// <remarks>
/// An explicit table of records with delegates, exactly like
/// <see cref="OverviewFields"/>. Nothing here is discovered by reflection, so it
/// survives NativeAOT trimming.
/// </remarks>
internal static class LanguageFields
{
	/// <summary>
	/// The spans rendered when <c>--languages-fields</c> is absent. This exact
	/// list, in this exact order, reproduces the legend the template emitted
	/// before the span set became configurable, byte for byte.
	/// </summary>
	public static readonly IReadOnlyList<string> Default =
	[
		"name",
		"percent",
	];

	/// <summary>Every available span, in the order <c>--help</c> and the README list them.</summary>
	public static readonly IReadOnlyList<LanguageField> All =
	[
		new("rank", "rank",
					static r => string.Create(CultureInfo.InvariantCulture, $"{r.Index + 1}.")),
		new("name", "lang",
					static r => r.Language.Name),
		new("percent", "percent",
					static r => Fixed(Percent(r), 2) + "%"),
		new("size", "size",
					static r => FormatBytes(r.Language.Size)),

		// Estimated, not measured: see LanguageLineEstimates for why GitHub cannot
		// supply the real figure. The leading '~' is part of the value on purpose.
		new("lines", "lines",
					static r => Lines(LanguageLineEstimates.Estimate(r.Language.Name, r.Language.Size))),
		new("repos", "repos",
					static r => Repos(r.Language.RepoCount)),
	];

	/// <summary>
	/// Looks up each id in <paramref name="ids"/>, preserving the caller's order.
	/// </summary>
	/// <exception cref="InvalidFieldException">
	/// An id is not in the catalogue, or is listed twice. Both are typos far more
	/// often than intent, and a silently dropped span is hard to notice in an image.
	/// </exception>
	public static IReadOnlyList<LanguageField> Resolve(IReadOnlyList<string> ids)
	{
		List<LanguageField> resolved = new(ids.Count);
		HashSet<string> seen = new(StringComparer.Ordinal);

		foreach (string id in ids)
		{
			LanguageField field = Find(id)
					?? throw new InvalidFieldException(id, $"Unknown languages field '{id}'. Valid fields: {Catalogue}.");

			if (!seen.Add(field.Id))
			{
				throw new InvalidFieldException(id, $"Languages field '{id}' is listed more than once.");
			}

			resolved.Add(field);
		}

		return resolved;
	}

	private static LanguageField? Find(string id)
	{
		foreach (LanguageField field in All)
		{
			if (string.Equals(field.Id, id, StringComparison.OrdinalIgnoreCase))
			{
				return field;
			}
		}

		return null;
	}

	private static string Catalogue => string.Join(", ", All.Select(static f => f.Id));

	// -- Formatters -------------------------------------------------------------
	//
	// All invariant, for the reason SvgTemplate.FormatNumber documents: a
	// culture-sensitive format here would rewrite every generated SVG on a de-DE
	// host.

	/// <summary>The language's share of the byte total, guarding the empty case.</summary>
	internal static double Percent(LanguageRow row) =>
			row.Total == 0 ? 0.0 : 100.0 * ((double)row.Language.Size / row.Total);

	internal static string Fixed(double value, int decimals) =>
			value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

	private const long BytesPerKilobyte = 1024;
	private const long BytesPerMegabyte = 1024 * 1024;
	private const long BytesPerGigabyte = 1024L * 1024 * 1024;

	/// <summary>
	/// Formats a raw byte count. Deliberately separate from
	/// <see cref="OverviewFields.FormatDiskUsage"/>, which takes kilobytes because
	/// that is the unit GitHub reports disk usage in; folding the two together
	/// would hide which unit each caller is holding.
	/// </summary>
	internal static string FormatBytes(long bytes) => bytes switch
	{
		>= BytesPerGigabyte => Scaled((double)bytes / BytesPerGigabyte, "GB"),
		>= BytesPerMegabyte => Scaled((double)bytes / BytesPerMegabyte, "MB"),
		>= BytesPerKilobyte => Scaled((double)bytes / BytesPerKilobyte, "KB"),
		_ => SvgTemplate.FormatNumber(bytes) + " B",
	};

	/// <summary>
	/// One decimal place, dropped when it is a zero: <c>768 KB</c> rather than
	/// <c>768.0 KB</c>. The legend is width constrained, and a trailing zero here
	/// carries no information.
	/// </summary>
	private static string Scaled(double value, string unit)
	{
		string number = value.ToString("F1", CultureInfo.InvariantCulture);

		if (number.EndsWith(".0", StringComparison.Ordinal))
		{
			number = number[..^2];
		}

		return number + " " + unit;
	}

	/// <summary>
	/// Abbreviates a count to at most five characters. The legend is width
	/// constrained in a way the overview table is not, so <c>41k</c> earns its
	/// place over the <c>41,000</c> that <see cref="SvgTemplate.FormatNumber"/>
	/// would produce.
	/// </summary>
	internal static string FormatCompact(long value) => value switch
	{
		>= 1_000_000 => Suffixed((double)value / 1_000_000, "M"),
		>= 10_000 => string.Create(CultureInfo.InvariantCulture, $"{value / 1000}k"),
		>= 1_000 => Suffixed((double)value / 1000, "k"),
		_ => SvgTemplate.FormatNumber(value),
	};

	private static string Suffixed(double value, string suffix) =>
			string.Create(CultureInfo.InvariantCulture, $"{value:F1}{suffix}");

	private static string Lines(long lines) =>
			string.Create(CultureInfo.InvariantCulture, $"~{FormatCompact(lines)} line{SvgTemplate.Plural(lines)}");

	private static string Repos(int repos) =>
			string.Create(CultureInfo.InvariantCulture, $"{SvgTemplate.FormatNumber(repos)} repo{SvgTemplate.Plural(repos)}");
}
