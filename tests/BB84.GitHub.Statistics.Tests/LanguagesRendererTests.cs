using BB84.GitHub.Statistics.Rendering;

namespace BB84.GitHub.Statistics.Tests;

/// <summary>
/// The expected strings here were taken from real output of the Zig
/// implementation (the <c>generated</c> branch), including its trailing spaces.
/// Reformatting the markup will break byte-compatibility with existing SVGs.
/// </summary>
[TestClass]
public sealed class LanguagesRendererTests
{
	private static readonly IReadOnlyList<LanguageField> DefaultFields =
			LanguageFields.Resolve(LanguageFields.Default);

	private static IReadOnlyList<LanguageField> Fields(params string[] ids) =>
			LanguageFields.Resolve(ids);

	[TestMethod]
	public void RenderProgressFragmentMatchesZigOutput()
	{
		LanguagesMarkup markup = LanguagesRenderer.Render(
				[new AggregatedLanguage("TSQL", 5705800, "#e38c00")],
				10000000,
				DefaultFields);

		Assert.AreEqual(
				"<span style=\"\n" +
				"  background-color: #e38c00; \n" +
				"  width: 57.058%;\n" +
				"\" class=\"progress-item\"></span>",
				markup.Progress);
	}

	/// <summary>
	/// The default field set exists to reproduce this exact markup. If this test
	/// needs new expected values, <see cref="LanguageFields.Default"/> is wrong.
	/// </summary>
	[TestMethod]
	public void RenderListFragmentMatchesZigOutput()
	{
		LanguagesMarkup markup = LanguagesRenderer.Render(
				[new AggregatedLanguage("TSQL", 5705800, "#e38c00")],
				10000000,
				DefaultFields);

		Assert.AreEqual(
				"<li style=\"animation-delay: 150ms;\">\n" +
				"  <svg \n" +
				"      xmlns=\"http://www.w3.org/2000/svg\" \n" +
				"      class=\"octicon\"\n" +
				"      style=\"fill: #e38c00;\" \n" +
				"      viewBox=\"0 0 16 16\" \n" +
				"      version=\"1.1\" \n" +
				"      width=\"16\" \n" +
				"      height=\"16\"\n" +
				"  ><path \n" +
				"      fill-rule=\"evenodd\" \n" +
				"      d=\"M8 4a4 4 0 100 8 4 4 0 000-8z\"\n" +
				"  ></path></svg>\n" +
				"  <span class=\"lang\">TSQL</span>\n" +
				"  <span class=\"percent\">57.06%</span>\n" +
				"</li>\n",
				markup.LangList);
	}

	[TestMethod]
	public void RenderAnimationDelayIncrementsBy150Ms()
	{
		LanguagesMarkup markup = LanguagesRenderer.Render(
				[
						new AggregatedLanguage("A", 1, "#111"),
								new AggregatedLanguage("B", 1, "#222"),
								new AggregatedLanguage("C", 1, "#333"),
						],
				3,
				DefaultFields);

		Assert.Contains("animation-delay: 150ms;", markup.LangList);
		Assert.Contains("animation-delay: 300ms;", markup.LangList);
		Assert.Contains("animation-delay: 450ms;", markup.LangList);
	}

	[TestMethod]
	public void RenderMissingColourFallsBackToBlack()
	{
		LanguagesMarkup markup = LanguagesRenderer.Render(
				[new AggregatedLanguage("Mystery", 1, null)],
				1,
				DefaultFields);

		Assert.Contains("background-color: #000; ", markup.Progress);
		Assert.Contains("style=\"fill: #000;\" ", markup.LangList);
	}

	/// <summary>A user whose languages are all excluded must not divide by zero.</summary>
	[TestMethod]
	public void RenderZeroTotalYieldsZeroPercent()
	{
		LanguagesMarkup markup = LanguagesRenderer.Render(
				[new AggregatedLanguage("Ghost", 5, "#abc")],
				0,
				DefaultFields);

		Assert.Contains("width: 0.000%;", markup.Progress);
		Assert.Contains("<span class=\"percent\">0.00%</span>", markup.LangList);
	}

	[TestMethod]
	public void RenderEmptyInputProducesEmptyFragments()
	{
		LanguagesMarkup markup = LanguagesRenderer.Render([], 0, DefaultFields);

		Assert.AreEqual(string.Empty, markup.Progress);
		Assert.AreEqual(string.Empty, markup.LangList);
	}

	[TestMethod]
	public void RenderUsesLineFeedsOnEveryPlatform()
	{
		LanguagesMarkup markup = LanguagesRenderer.Render(
				[new AggregatedLanguage("A", 1, "#111")],
				1,
				DefaultFields);

		Assert.DoesNotContain('\r', markup.LangList, "SVG output must not contain CRLF on Windows builds");
	}

	// -- Fields -----------------------------------------------------------------

	[TestMethod]
	public void RenderEmitsEverySelectedFieldInTheGivenOrder()
	{
		LanguagesMarkup markup = LanguagesRenderer.Render(
				[new AggregatedLanguage("C#", 2_097_152, "#178600", 7)],
				4_194_304,
				Fields("rank", "name", "percent", "size", "lines", "repos"));

		Assert.Contains(
				"  <span class=\"rank\">1.</span>\n" +
				"  <span class=\"lang\">C#</span>\n" +
				"  <span class=\"percent\">50.00%</span>\n" +
				"  <span class=\"size\">2 MB</span>\n" +
				"  <span class=\"lines\">~67k lines</span>\n" +
				"  <span class=\"repos\">7 repos</span>\n",
				markup.LangList);
	}

	[TestMethod]
	public void RenderRankFollowsListPosition()
	{
		LanguagesMarkup markup = LanguagesRenderer.Render(
				[
						new AggregatedLanguage("A", 3, "#111"),
								new AggregatedLanguage("B", 2, "#222"),
								new AggregatedLanguage("C", 1, "#333"),
						],
				6,
				Fields("rank"));

		Assert.Contains("<span class=\"rank\">1.</span>", markup.LangList);
		Assert.Contains("<span class=\"rank\">2.</span>", markup.LangList);
		Assert.Contains("<span class=\"rank\">3.</span>", markup.LangList);
	}

	[TestMethod]
	public void RenderSingularReposAndLines()
	{
		LanguagesMarkup markup = LanguagesRenderer.Render(
				[new AggregatedLanguage("C#", 31, "#178600", 1)],
				31,
				Fields("lines", "repos"));

		Assert.Contains("<span class=\"lines\">~1 line</span>", markup.LangList);
		Assert.Contains("<span class=\"repos\">1 repo</span>", markup.LangList);
	}

	/// <summary>
	/// An SVG is parsed as XML, so an unescaped <c>&amp;</c> in a language name
	/// makes the whole document fail to render rather than merely look wrong.
	/// </summary>
	[TestMethod]
	public void RenderEscapesLanguageNames()
	{
		LanguagesMarkup markup = LanguagesRenderer.Render(
				[new AggregatedLanguage("Rock & Roll<script>", 1, "#111")],
				1,
				Fields("name"));

		Assert.Contains("<span class=\"lang\">Rock &amp; Roll&lt;script&gt;</span>", markup.LangList);
	}

	// -- Summary ----------------------------------------------------------------

	[TestMethod]
	public void RenderSummaryReportsCountSizeAndEstimatedLines()
	{
		LanguagesMarkup markup = LanguagesRenderer.Render(
				[
						new AggregatedLanguage("C#", 3_145_728, "#178600", 4),
								new AggregatedLanguage("Markdown", 1_048_576, "#083fa1", 2),
						],
				4_194_304,
				DefaultFields);

		// 3,145,728 / 31 = 101,475 and 1,048,576 / 50 = 20,971, so 122,446 in total.
		Assert.AreEqual("2 languages · 4 MB · ~122k lines", markup.Summary);
	}

	[TestMethod]
	public void RenderSummaryHandlesTheSingularAndEmptyCases()
	{
		Assert.StartsWith(
				"1 language · ",
				LanguagesRenderer.Render([new AggregatedLanguage("C#", 31, "#178600", 1)], 31, DefaultFields).Summary);

		Assert.AreEqual("No languages", LanguagesRenderer.Render([], 0, DefaultFields).Summary);
	}

	// -- Geometry ---------------------------------------------------------------

	[TestMethod]
	public void RenderHeightGrowsWithTheNumberOfLanguages()
	{
		int Height(int count) => LanguagesRenderer.Render(
				[.. Enumerable.Range(0, count).Select(i => new AggregatedLanguage($"Language{i}", 100, "#111", 1))],
				100L * count,
				DefaultFields).Height;

		Assert.IsGreaterThan(Height(4), Height(20));
		Assert.IsGreaterThan(Height(20), Height(60));
	}

	/// <summary>
	/// More spans per language means wider entries, fewer per wrapped line, and so
	/// a taller card. This is the reason the height cannot simply count languages.
	/// </summary>
	[TestMethod]
	public void RenderHeightGrowsWithWiderFieldSets()
	{
		AggregatedLanguage[] languages =
				[.. Enumerable.Range(0, 12).Select(i => new AggregatedLanguage($"Language{i}", 1_000_000, "#111", 3))];

		int narrow = LanguagesRenderer.Render(languages, 12_000_000, DefaultFields).Height;
		int wide = LanguagesRenderer.Render(
				languages,
				12_000_000,
				Fields("rank", "name", "percent", "size", "lines", "repos")).Height;

		Assert.IsGreaterThan(narrow, wide);
	}

	[TestMethod]
	public void RenderInnerHeightIsInsetFromTheOuterHeight()
	{
		LanguagesMarkup markup = LanguagesRenderer.Render(
				[new AggregatedLanguage("C#", 100, "#178600", 1)],
				100,
				DefaultFields);

		Assert.AreEqual(34, markup.Height - markup.InnerHeight);
	}

	/// <summary>
	/// One short language must still reserve a legend line, and no languages at all
	/// must still leave room for the heading, the summary and the bar.
	/// </summary>
	[TestMethod]
	public void RenderHeightIsSaneAtTheBoundaries()
	{
		int single = LanguagesRenderer.Render(
				[new AggregatedLanguage("C#", 100, "#178600", 1)],
				100,
				DefaultFields).Height;

		int none = LanguagesRenderer.Render([], 0, DefaultFields).Height;

		Assert.AreEqual(153, single, "chrome (105) + summary (27) + one 21px legend line");
		Assert.AreEqual(132, none, "chrome (105) + summary (27), with no legend lines");
	}
}
