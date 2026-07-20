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
	[TestMethod]
	public void RenderProgressFragmentMatchesZigOutput()
	{
		(string progress, _) = LanguagesRenderer.Render(
				[new AggregatedLanguage("TSQL", 5705800, "#e38c00")],
				10000000);

		Assert.AreEqual(
				"<span style=\"\n" +
				"  background-color: #e38c00; \n" +
				"  width: 57.058%;\n" +
				"\" class=\"progress-item\"></span>",
				progress);
	}

	[TestMethod]
	public void RenderListFragmentMatchesZigOutput()
	{
		(_, string langList) = LanguagesRenderer.Render(
				[new AggregatedLanguage("TSQL", 5705800, "#e38c00")],
				10000000);

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
				langList);
	}

	[TestMethod]
	public void RenderAnimationDelayIncrementsBy150Ms()
	{
		(_, string langList) = LanguagesRenderer.Render(
				[
						new AggregatedLanguage("A", 1, "#111"),
								new AggregatedLanguage("B", 1, "#222"),
								new AggregatedLanguage("C", 1, "#333"),
						],
				3);

		Assert.Contains("animation-delay: 150ms;", langList);
		Assert.Contains("animation-delay: 300ms;", langList);
		Assert.Contains("animation-delay: 450ms;", langList);
	}

	[TestMethod]
	public void RenderMissingColourFallsBackToBlack()
	{
		(string progress, string langList) = LanguagesRenderer.Render(
				[new AggregatedLanguage("Mystery", 1, null)],
				1);

		Assert.Contains("background-color: #000; ", progress);
		Assert.Contains("style=\"fill: #000;\" ", langList);
	}

	/// <summary>A user whose languages are all excluded must not divide by zero.</summary>
	[TestMethod]
	public void RenderZeroTotalYieldsZeroPercent()
	{
		(string progress, string langList) = LanguagesRenderer.Render(
				[new AggregatedLanguage("Ghost", 5, "#abc")],
				0);

		Assert.Contains("width: 0.000%;", progress);
		Assert.Contains("<span class=\"percent\">0.00%</span>", langList);
	}

	[TestMethod]
	public void RenderEmptyInputProducesEmptyFragments()
	{
		(string progress, string langList) = LanguagesRenderer.Render([], 0);

		Assert.AreEqual(string.Empty, progress);
		Assert.AreEqual(string.Empty, langList);
	}

	[TestMethod]
	public void RenderUsesLineFeedsOnEveryPlatform()
	{
		(_, string langList) = LanguagesRenderer.Render([new AggregatedLanguage("A", 1, "#111")], 1);

		Assert.DoesNotContain('\r', langList, "SVG output must not contain CRLF on Windows builds");
	}
}
