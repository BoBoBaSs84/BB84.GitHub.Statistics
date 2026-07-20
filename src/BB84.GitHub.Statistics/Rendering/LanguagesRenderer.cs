using System.Globalization;
using System.Text;

namespace BB84.GitHub.Statistics.Rendering;

/// <summary>A language row in the aggregated totals, already sorted by size descending.</summary>
internal readonly record struct AggregatedLanguage(string Name, long Size, string? Color);

/// <summary>
/// Builds the two HTML fragments injected into <c>languages.svg</c>: the stacked
/// progress bar and the legend list.
/// </summary>
/// <remarks>
/// Byte-for-byte compatible with the Zig implementation, including the trailing
/// spaces inside the markup. Newlines are hard-coded to <c>\n</c> rather than
/// <see cref="Environment.NewLine"/> so Windows builds produce identical SVGs.
/// </remarks>
internal static class LanguagesRenderer
{
	private const string DefaultColor = "#000";

	public static (string Progress, string LangList) Render(
			IReadOnlyList<AggregatedLanguage> languages,
			long languagesTotal)
	{
		StringBuilder progress = new();
		StringBuilder langList = new();

		for (int i = 0; i < languages.Count; i++)
		{
			AggregatedLanguage language = languages[i];
			double percent = languagesTotal == 0
					? 0.0
					: 100.0 * ((double)language.Size / languagesTotal);
			string color = language.Color ?? DefaultColor;

			_ = progress.Append("<span style=\"\n")
					.Append("  background-color: ").Append(color).Append("; \n")
					.Append("  width: ").Append(Fixed(percent, 3)).Append("%;\n")
					.Append("\" class=\"progress-item\"></span>");

			_ = langList.Append("<li style=\"animation-delay: ")
					.Append(((long)(i + 1) * 150).ToString(CultureInfo.InvariantCulture))
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
					.Append("  ></path></svg>\n")
					.Append("  <span class=\"lang\">").Append(language.Name).Append("</span>\n")
					.Append("  <span class=\"percent\">").Append(Fixed(percent, 2)).Append("%</span>\n")
					.Append("</li>\n");
		}

		return (progress.ToString(), langList.ToString());
	}

	private static string Fixed(double value, int decimals) =>
			value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}
