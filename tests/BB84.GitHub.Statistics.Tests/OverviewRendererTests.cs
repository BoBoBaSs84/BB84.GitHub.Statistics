using BB84.GitHub.Statistics.Rendering;

namespace BB84.GitHub.Statistics.Tests;

/// <summary>
/// Covers the table body injected into <c>overview.svg</c>.
/// </summary>
/// <remarks>
/// The golden strings below are the six rows exactly as they were hard-coded in
/// the template before the row set became configurable, with the placeholders
/// replaced by <see cref="Stats"/>'s values. Rendering the default field set has
/// to keep reproducing them byte for byte, blank lines included: every generated
/// README image in the wild depends on it.
/// </remarks>
[TestClass]
public sealed class OverviewRendererTests
{
	private static readonly AggregateStats Stats = new(
			Name: "Ada",
			Contributions: 78901,
			Stars: 1234,
			Forks: 56,
			LinesChanged: 234567,
			Views: 89,
			Repos: 12,
			LanguagesTotal: 0,
			Languages: [],
			Breakdown: new ContributionBreakdown(
					Commits: 500, Prs: 60, Reviews: 7, Issues: 8, ReposCreated: 9),
			Profile: new ProfileStats(
					Followers: 321,
					Following: 21,
					StarsGiven: 400,
					Gists: 3,
					Organizations: 2,
					Watching: 44,
					Sponsors: 1,
					MergedPullRequests: 55,
					OwnRepositories: 66,
					DiskUsageKb: 2_097_152,
					CreatedAt: "2013-03-04T10:00:00Z"),
			Streaks: new StreakStats(
					Current: 5,
					Longest: 88,
					BusiestDay: "2024-03-14",
					BusiestDayCount: 37,
					ThisYear: 1500,
					Private: 250));

	private static readonly string[] DefaultRows =
	[
		"""<tr><td><svg class="octicon" viewBox="0 0 16 16" xmlns="http://www.w3.org/2000/svg" version="1.1" width="16" height="16"><path fill-rule="evenodd" d="M8 .25a.75.75 0 01.673.418l1.882 3.815 4.21.612a.75.75 0 01.416 1.279l-3.046 2.97.719 4.192a.75.75 0 01-1.088.791L8 12.347l-3.766 1.98a.75.75 0 01-1.088-.79l.72-4.194L.818 6.374a.75.75 0 01.416-1.28l4.21-.611L7.327.668A.75.75 0 018 .25zm0 2.445L6.615 5.5a.75.75 0 01-.564.41l-3.097.45 2.24 2.184a.75.75 0 01.216.664l-.528 3.084 2.769-1.456a.75.75 0 01.698 0l2.77 1.456-.53-3.084a.75.75 0 01.216-.664l2.24-2.183-3.096-.45a.75.75 0 01-.564-.41L8 2.694v.001z"></path></svg>Stars</td><td>1,234</td></tr>""",
		"""<tr style="animation-delay: 150ms"><td><svg class="octicon" viewBox="0 0 16 16" xmlns="http://www.w3.org/2000/svg" version="1.1" width="16" height="16" role="img"><path fill-rule="evenodd" d="M5 3.25a.75.75 0 11-1.5 0 .75.75 0 011.5 0zm0 2.122a2.25 2.25 0 10-1.5 0v.878A2.25 2.25 0 005.75 8.5h1.5v2.128a2.251 2.251 0 101.5 0V8.5h1.5a2.25 2.25 0 002.25-2.25v-.878a2.25 2.25 0 10-1.5 0v.878a.75.75 0 01-.75.75h-4.5A.75.75 0 015 6.25v-.878zm3.75 7.378a.75.75 0 11-1.5 0 .75.75 0 011.5 0zm3-8.75a.75.75 0 100-1.5.75.75 0 000 1.5z"></path></svg>Forks</td><td>56</td></tr>""",
		"""<tr style="animation-delay: 300ms"><td><svg class="octicon" viewBox="0 0 16 16" xmlns="http://www.w3.org/2000/svg" version="1.1" width="16" height="16" aria-hidden="true"><path fill-rule="evenodd" d="M1 2.5A2.5 2.5 0 013.5 0h8.75a.75.75 0 01.75.75v3.5a.75.75 0 01-1.5 0V1.5h-8a1 1 0 00-1 1v6.708A2.492 2.492 0 013.5 9h3.25a.75.75 0 010 1.5H3.5a1 1 0 100 2h5.75a.75.75 0 010 1.5H3.5A2.5 2.5 0 011 11.5v-9zm13.23 7.79a.75.75 0 001.06-1.06l-2.505-2.505a.75.75 0 00-1.06 0L9.22 9.229a.75.75 0 001.06 1.061l1.225-1.224v6.184a.75.75 0 001.5 0V9.066l1.224 1.224z"></path></svg>All-time contributions</td><td>78,901</td></tr>""",
		"""<tr style="animation-delay: 450ms"><td><svg class="octicon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" width="16" height="16"><path fill-rule="evenodd" d="M8.75 1.75a.75.75 0 00-1.5 0V5H4a.75.75 0 000 1.5h3.25v3.25a.75.75 0 001.5 0V6.5H12A.75.75 0 0012 5H8.75V1.75zM4 13a.75.75 0 000 1.5h8a.75.75 0 100-1.5H4z"></path></svg>Lines of code changed</td><td>234,567</td></tr>""",
		"""<tr style="animation-delay: 600ms"><td><svg class="octicon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" width="16" height="16"><path fill-rule="evenodd" d="M1.679 7.932c.412-.621 1.242-1.75 2.366-2.717C5.175 4.242 6.527 3.5 8 3.5c1.473 0 2.824.742 3.955 1.715 1.124.967 1.954 2.096 2.366 2.717a.119.119 0 010 .136c-.412.621-1.242 1.75-2.366 2.717C10.825 11.758 9.473 12.5 8 12.5c-1.473 0-2.824-.742-3.955-1.715C2.92 9.818 2.09 8.69 1.679 8.068a.119.119 0 010-.136zM8 2c-1.981 0-3.67.992-4.933 2.078C1.797 5.169.88 6.423.43 7.1a1.619 1.619 0 000 1.798c.45.678 1.367 1.932 2.637 3.024C4.329 13.008 6.019 14 8 14c1.981 0 3.67-.992 4.933-2.078 1.27-1.091 2.187-2.345 2.637-3.023a1.619 1.619 0 000-1.798c-.45-.678-1.367-1.932-2.637-3.023C11.671 2.992 9.981 2 8 2zm0 8a2 2 0 100-4 2 2 0 000 4z"></path></svg>Repository views (past two weeks)</td><td>89</td></tr>""",
		"""<tr style="animation-delay: 750ms"><td><svg class="octicon" viewBox="0 0 16 16" xmlns="http://www.w3.org/2000/svg" version="1.1" width="16" height="16" aria-hidden="true"><path fill-rule="evenodd" d="M2 2.5A2.5 2.5 0 014.5 0h8.75a.75.75 0 01.75.75v12.5a.75.75 0 01-.75.75h-2.5a.75.75 0 110-1.5h1.75v-2h-8a1 1 0 00-.714 1.7.75.75 0 01-1.072 1.05A2.495 2.495 0 012 11.5v-9zm10.5-1V9h-8c-.356 0-.694.074-1 .208V2.5a1 1 0 011-1h8zM5 12.25v3.25a.25.25 0 00.4.2l1.45-1.087a.25.25 0 01.3 0L8.6 15.7a.25.25 0 00.4-.2v-3.25a.25.25 0 00-.25-.25h-3.5a.25.25 0 00-.25.25z"></path></svg>Repositories with contributions</td><td>12</td></tr>""",
	];

	private static OverviewMarkup RenderDefault() =>
			OverviewRenderer.Render(OverviewFields.Resolve(OverviewFields.Default), Stats);

	private static OverviewMarkup Render(params string[] ids) =>
			OverviewRenderer.Render(OverviewFields.Resolve(ids), Stats);

	/// <summary>The regression guard this whole change is measured against.</summary>
	[TestMethod]
	public void RenderDefaultFieldsReproducesTheOriginalRows() =>
			Assert.AreEqual(
					"\n" + string.Join("\n\n", DefaultRows) + "\n",
					RenderDefault().Rows,
					"the default row set must stay byte-identical to the hard-coded template");

	/// <summary>
	/// The blank lines are load-bearing for the byte comparison above, so they are
	/// pinned separately rather than only implied by the golden string.
	/// </summary>
	[TestMethod]
	public void RenderSurroundsRowsWithBlankLines()
	{
		string rows = RenderDefault().Rows;

		Assert.StartsWith("\n<tr>", rows);
		Assert.EndsWith("</tr>\n", rows);
		Assert.Contains("</tr>\n\n<tr ", rows, StringComparison.Ordinal);
	}

	[TestMethod]
	public void RenderDefaultFieldsKeepsTheOriginalDimensions()
	{
		OverviewMarkup markup = RenderDefault();

		Assert.AreEqual(210, markup.Height);
		Assert.AreEqual(168, markup.InnerHeight);
	}

	[TestMethod]
	[DataRow(1, 90, 48)]
	[DataRow(2, 114, 72)]
	[DataRow(6, 210, 168)]
	[DataRow(12, 354, 312)]
	public void RenderGrowsHeightWithTheRowCount(int count, int expectedHeight, int expectedInnerHeight)
	{
		string[] ids = [.. OverviewFields.All.Take(count).Select(static f => f.Id)];

		OverviewMarkup markup = Render(ids);

		Assert.AreEqual(expectedHeight, markup.Height);
		Assert.AreEqual(expectedInnerHeight, markup.InnerHeight);
	}

	/// <summary>The caller's order is the rendered order, not the catalogue's.</summary>
	[TestMethod]
	public void RenderHonoursTheRequestedOrder()
	{
		string rows = Render("repos", "stars", "followers").Rows;

		Assert.IsTrue(
				rows.IndexOf("Repositories with contributions", StringComparison.Ordinal)
				< rows.IndexOf("Stars", StringComparison.Ordinal),
				"repos was requested first");

		Assert.IsTrue(
				rows.IndexOf("Stars", StringComparison.Ordinal)
				< rows.IndexOf("Followers", StringComparison.Ordinal),
				"stars was requested before followers");
	}

	/// <summary>The first row animates immediately and carries no style attribute.</summary>
	[TestMethod]
	public void RenderStaggersAnimationFromTheSecondRow()
	{
		string rows = Render("stars", "forks", "repos").Rows;

		Assert.Contains("\n<tr><td>", rows, StringComparison.Ordinal);
		Assert.Contains("""<tr style="animation-delay: 150ms">""", rows, StringComparison.Ordinal);
		Assert.Contains("""<tr style="animation-delay: 300ms">""", rows, StringComparison.Ordinal);
		Assert.DoesNotContain("""animation-delay: 0ms""", rows, StringComparison.Ordinal);
	}

	[TestMethod]
	public void RenderSingleFieldEmitsOneRow()
	{
		OverviewMarkup markup = Render("stars");

		Assert.AreEqual(1, markup.Rows.Split("<tr", StringSplitOptions.None).Length - 1);
		Assert.Contains("1,234", markup.Rows, StringComparison.Ordinal);
		Assert.DoesNotContain("animation-delay", markup.Rows, StringComparison.Ordinal);
	}

	/// <summary>
	/// Only the newlines that hug the placeholder, leaving an empty table body.
	/// Not reachable from the CLI, where an empty <c>--overview-fields</c> falls
	/// back to the default set, but the renderer should not fall over on it.
	/// </summary>
	[TestMethod]
	public void RenderNoFieldsEmitsNoRows()
	{
		OverviewMarkup markup = OverviewRenderer.Render([], Stats);

		Assert.AreEqual("\n\n", markup.Rows);
		Assert.DoesNotContain("<tr", markup.Rows, StringComparison.Ordinal);
		Assert.AreEqual(66, markup.Height);
	}

	/// <summary>
	/// An SVG is parsed as XML, so an unescaped ampersand does not merely look
	/// wrong, it stops the whole image rendering.
	/// </summary>
	[TestMethod]
	public void RenderEscapesMarkupInLabelsAndValues()
	{
		OverviewField field = new("custom", "R&D <team>", "<svg></svg>", static _ => "a < b & c > d");

		string rows = OverviewRenderer.Render([field], Stats).Rows;

		Assert.Contains("R&amp;D &lt;team&gt;", rows, StringComparison.Ordinal);
		Assert.Contains("a &lt; b &amp; c &gt; d", rows, StringComparison.Ordinal);
	}

	/// <summary>Windows builds must not smuggle CRLF into the generated SVG.</summary>
	[TestMethod]
	public void RenderNeverEmitsCarriageReturns() =>
			Assert.DoesNotContain('\r', RenderDefault().Rows, "SVG output must not contain CRLF on Windows builds");
}
