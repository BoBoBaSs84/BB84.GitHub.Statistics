using System.Globalization;
using System.Text;

namespace BB84.GitHub.Statistics.Rendering;

/// <summary>A language row in the aggregated totals, already sorted by size descending.</summary>
/// <param name="Name">The name GitHub's linguist reports, e.g. <c>C#</c>.</param>
/// <param name="Size">Bytes attributed to the language across every counted repository.</param>
/// <param name="Color">Linguist's hex colour, or <see langword="null"/> when it has none.</param>
/// <param name="RepoCount">
/// How many counted repositories report the language. Defaulted so the older
/// three-argument construction still compiles.
/// </param>
internal readonly record struct AggregatedLanguage(
		string Name,
		long Size,
		string? Color,
		int RepoCount = 0);

/// <summary>The pieces of <c>languages.svg</c> that depend on the aggregated languages.</summary>
/// <param name="Progress">The stacked bar's <c>&lt;span&gt;</c> segments.</param>
/// <param name="LangList">The legend's <c>&lt;li&gt;</c> markup.</param>
/// <param name="Summary">The totals line under the heading.</param>
/// <param name="Height">Height of the root <c>&lt;svg&gt;</c>.</param>
/// <param name="InnerHeight">Height of the <c>&lt;foreignObject&gt;</c>.</param>
internal readonly record struct LanguagesMarkup(
		string Progress,
		string LangList,
		string Summary,
		int Height,
		int InnerHeight);

/// <summary>
/// Builds the fragments injected into <c>languages.svg</c>: the stacked progress
/// bar, the legend list, the totals line, and the card geometry those imply.
/// </summary>
/// <remarks>
/// With the default field set the legend is byte-for-byte compatible with the Zig
/// implementation, including the trailing spaces inside the markup. Newlines are
/// hard-coded to <c>\n</c> rather than <see cref="Environment.NewLine"/> so
/// Windows builds produce identical SVGs.
/// </remarks>
internal static class LanguagesRenderer
{
	private const string DefaultColor = "#000";

	/// <summary>Matches the stagger the original hand-written legend used.</summary>
	private const int AnimationStepMs = 150;

	public static LanguagesMarkup Render(
			IReadOnlyList<AggregatedLanguage> languages,
			long languagesTotal,
			IReadOnlyList<LanguageField> fields)
	{
		StringBuilder progress = new();
		StringBuilder langList = new();

		// Kept so the geometry pass measures exactly the text that was emitted.
		List<string[]> renderedValues = new(languages.Count);

		for (int i = 0; i < languages.Count; i++)
		{
			AggregatedLanguage language = languages[i];
			LanguageRow row = new(language, i, languagesTotal);
			string color = language.Color ?? DefaultColor;

			_ = progress.Append("<span style=\"\n")
					.Append("  background-color: ").Append(color).Append("; \n")
					.Append("  width: ").Append(LanguageFields.Fixed(LanguageFields.Percent(row), 3)).Append("%;\n")
					.Append("\" class=\"progress-item\"></span>");

			_ = langList.Append("<li style=\"animation-delay: ")
					.Append(((long)(i + 1) * AnimationStepMs).ToString(CultureInfo.InvariantCulture))
					.Append("ms;\">\n")
					.Append("  <svg \n")
					.Append("      xmlns=\"http://www.w3.org/2000/svg\" \n")
					.Append("      class=\"octicon\"\n")
					.Append("      style=\"fill: ").Append(color).Append(";\" \n")
					.Append("      viewBox=\"0 0 16 16\" \n")
					.Append("      version=\"1.1\" \n")
					.Append("      width=\"16\" \n")
					.Append("      height=\"16\"\n")
					.Append("  ><path \n")
					.Append("      fill-rule=\"evenodd\" \n")
					.Append("      d=\"M8 4a4 4 0 100 8 4 4 0 000-8z\"\n")
					.Append("  ></path></svg>\n");

			string[] values = new string[fields.Count];

			for (int f = 0; f < fields.Count; f++)
			{
				LanguageField field = fields[f];
				values[f] = SvgTemplate.Escape(field.Value(row));

				_ = langList.Append("  <span class=\"").Append(field.CssClass).Append("\">")
						.Append(values[f])
						.Append("</span>\n");
			}

			renderedValues.Add(values);

			_ = langList.Append("</li>\n");
		}

		int rows = EstimateRowCount(renderedValues);
		int height = ChromeHeight + SummaryHeight + (LineHeight * rows);

		return new LanguagesMarkup(
				progress.ToString(),
				langList.ToString(),
				BuildSummary(languages, languagesTotal),
				height,
				height - InnerInset);
	}

	// -- Summary ----------------------------------------------------------------

	private static string BuildSummary(IReadOnlyList<AggregatedLanguage> languages, long languagesTotal)
	{
		if (languages.Count == 0)
		{
			return "No languages";
		}

		long estimatedLines = 0;
		foreach (AggregatedLanguage language in languages)
		{
			estimatedLines += LanguageLineEstimates.Estimate(language.Name, language.Size);
		}

		string count = string.Create(
				CultureInfo.InvariantCulture,
				$"{SvgTemplate.FormatNumber(languages.Count)} language{SvgTemplate.Plural(languages.Count)}");

		string lines = string.Create(
				CultureInfo.InvariantCulture,
				$"~{LanguageFields.FormatCompact(estimatedLines)} line{SvgTemplate.Plural(estimatedLines)}");

		// Each part is machine-generated, but escaped anyway so the separator is the
		// only literal markup this string contributes.
		return string.Join(
				" · ",
				SvgTemplate.Escape(count),
				SvgTemplate.Escape(LanguageFields.FormatBytes(languagesTotal)),
				SvgTemplate.Escape(lines));
	}

	// -- Geometry ---------------------------------------------------------------
	//
	// Unlike the overview table, the legend is `li { display: inline-flex }` inside
	// a wrapping `ul`, so the number of rendered lines depends on how wide the text
	// is, not on how many languages there are. The card therefore has to guess at
	// text metrics. Every constant below deliberately errs high: a card that is a
	// little too tall shows whitespace, one that is too short silently clips, which
	// is the bug this replaces.

	/// <summary>The <c>&lt;foreignObject&gt;</c> width, matching the template.</summary>
	private const double ContentWidth = 318;

	/// <summary>The <c>svg</c> line-height, and so the height of one wrapped line.</summary>
	private const int LineHeight = 21;

	/// <summary>The 16px octicon plus its <c>0.5ch</c> right margin.</summary>
	private const double IconWidth = 19;

	/// <summary>The <c>margin-right</c> on each legend span.</summary>
	private const double SpanGap = 4;

	/// <summary>The <c>li { margin-right: 1ch }</c> at the legend's 12px font size.</summary>
	private const double ItemGap = 6;

	/// <summary>
	/// Average glyph width at 12px in the template's system font stack. Set above
	/// the true proportional average so the bold <c>.lang</c> span, which is wider,
	/// is covered without measuring each span separately.
	/// </summary>
	private const double CharWidth = 6.8;

	/// <summary>
	/// Everything that is not a legend line: the 42px <c>&lt;foreignObject&gt;</c>
	/// inset, the 36px heading, the 22px progress bar, and 13px of bottom padding.
	/// </summary>
	private const int ChromeHeight = 105;

	/// <summary>
	/// The totals text under the heading: one 21px line plus its <c>0.5em</c>
	/// bottom margin at the 12px font size.
	/// </summary>
	private const int SummaryHeight = 27;

	/// <summary>The <c>&lt;foreignObject&gt;</c> inset, 17px top and 17px bottom.</summary>
	private const int InnerInset = 34;

	/// <summary>
	/// Greedily packs the legend entries into <see cref="ContentWidth"/> the way a
	/// wrapping flex container would, and reports how many lines that takes.
	/// </summary>
	private static int EstimateRowCount(List<string[]> renderedValues)
	{
		if (renderedValues.Count == 0)
		{
			return 0;
		}

		int rows = 1;
		double used = 0;

		foreach (string[] values in renderedValues)
		{
			double width = IconWidth + ItemGap;

			foreach (string value in values)
			{
				width += (CharWidth * value.Length) + SpanGap;
			}

			// A single entry wider than the container still occupies its own line.
			if (used > 0 && used + width > ContentWidth)
			{
				rows++;
				used = width;
			}
			else
			{
				used += width;
			}
		}

		return rows;
	}
}
