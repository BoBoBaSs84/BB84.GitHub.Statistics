using BB84.GitHub.Statistics.Rendering;
using System.Globalization;

namespace BB84.GitHub.Statistics.Tests;

/// <summary>Covers the <c>--overview-fields</c> catalogue and its value formatters.</summary>
[TestClass]
public sealed class OverviewFieldsTests
{
	[TestMethod]
	public void DefaultIsTheOriginalSixRowsInOrder()
	{
		string[] expected = ["stars", "forks", "contributions", "lines_changed", "views", "repos"];

		Assert.AreSequenceEqual(expected, [.. OverviewFields.Default]);
	}

	[TestMethod]
	public void ResolveKeepsTheRequestedOrder()
	{
		string[] expected = ["repos", "stars"];

		Assert.AreSequenceEqual(
				expected, [.. OverviewFields.Resolve(["repos", "stars"]).Select(static f => f.Id)]);
	}

	[TestMethod]
	public void ResolveAcceptsEveryCatalogueField()
	{
		string[] ids = [.. OverviewFields.All.Select(static f => f.Id)];

		Assert.HasCount(ids.Length, OverviewFields.Resolve(ids));
	}

	/// <summary>Field ids are matched the same way option names are: case-insensitively.</summary>
	[TestMethod]
	public void ResolveIsCaseInsensitive() =>
			Assert.AreEqual("stars", OverviewFields.Resolve(["STARS"])[0].Id);

	[TestMethod]
	public void ResolveUnknownFieldThrowsAndNamesIt()
	{
		InvalidFieldException e = Assert.ThrowsExactly<InvalidFieldException>(
				() => OverviewFields.Resolve(["stars", "nope"]));

		Assert.AreEqual("nope", e.Field);
		Assert.Contains("lines_changed", e.Message, StringComparison.Ordinal, "the message should list the valid fields");
	}

	/// <summary>
	/// A duplicate is a typo far more often than intent, and a row silently
	/// rendered twice is easy to miss in a generated image.
	/// </summary>
	[TestMethod]
	public void ResolveDuplicateFieldThrows()
	{
		InvalidFieldException e = Assert.ThrowsExactly<InvalidFieldException>(
				() => OverviewFields.Resolve(["stars", "forks", "stars"]));

		Assert.AreEqual("stars", e.Field);
	}

	[TestMethod]
	public void ResolveEmptyListProducesNoFields() =>
			Assert.IsEmpty(OverviewFields.Resolve([]));

	[TestMethod]
	public void CatalogueIdsAreUnique() =>
			Assert.HasCount(
					OverviewFields.All.Count,
					OverviewFields.All.Select(static f => f.Id).Distinct(StringComparer.Ordinal).ToList());

	/// <summary>Every icon has to be a complete element, since it is emitted verbatim.</summary>
	[TestMethod]
	public void CatalogueIconsAreWellFormedSvgElements()
	{
		foreach (OverviewField field in OverviewFields.All)
		{
			Assert.StartsWith("<svg ", field.Icon, $"{field.Id} icon");
			Assert.EndsWith("</svg>", field.Icon, $"{field.Id} icon");
			Assert.Contains("""class="octicon" """.TrimEnd(), field.Icon, $"{field.Id} icon");
		}
	}

	// -- Formatters -------------------------------------------------------------

	[TestMethod]
	[DataRow(0L, "0 KB")]
	[DataRow(999L, "999 KB")]
	[DataRow(1023L, "1,023 KB")]
	[DataRow(1024L, "1.0 MB")]
	[DataRow(1536L, "1.5 MB")]
	[DataRow(1048576L, "1.0 GB")]
	[DataRow(2621440L, "2.5 GB")]
	public void FormatDiskUsageScalesFromKilobytes(long kilobytes, string expected) =>
			Assert.AreEqual(expected, OverviewFields.FormatDiskUsage(kilobytes));

	[TestMethod]
	[DataRow(0L, "0 days")]
	[DataRow(1L, "1 day")]
	[DataRow(2L, "2 days")]
	[DataRow(1234L, "1,234 days")]
	public void FormatDaysPluralisesAndGroups(long days, string expected) =>
			Assert.AreEqual(expected, OverviewFields.FormatDays(days));

	[TestMethod]
	public void FormatMemberSinceUsesAnInvariantMonthName() =>
			Assert.AreEqual("March 2013", OverviewFields.FormatMemberSince("2013-03-04T10:00:00Z"));

	[TestMethod]
	[DataRow(null)]
	[DataRow("")]
	[DataRow("not a date")]
	public void FormatMemberSinceFallsBackWhenTheProfileQueryFailed(string? createdAt) =>
			Assert.AreEqual("unknown", OverviewFields.FormatMemberSince(createdAt));

	[TestMethod]
	public void FormatAccountAgeFallsBackWhenTheProfileQueryFailed() =>
			Assert.AreEqual("unknown", OverviewFields.FormatAccountAge(null));

	[TestMethod]
	public void FormatAccountAgeCountsWholeYears()
	{
		string age = OverviewFields.FormatAccountAge(
				DateTimeOffset.UtcNow.AddYears(-3).AddDays(-1).ToString("O", CultureInfo.InvariantCulture));

		Assert.AreEqual("3 years", age);
	}

	[TestMethod]
	public void FormatAccountAgeReportsMonthsBelowAYear()
	{
		string age = OverviewFields.FormatAccountAge(
				DateTimeOffset.UtcNow.AddMonths(-5).AddDays(-1).ToString("O", CultureInfo.InvariantCulture));

		Assert.AreEqual("5 months", age);
	}

	[TestMethod]
	public void FormatAccountAgeSingularAtExactlyOneYear()
	{
		string age = OverviewFields.FormatAccountAge(
				DateTimeOffset.UtcNow.AddYears(-1).AddDays(-1).ToString("O", CultureInfo.InvariantCulture));

		Assert.AreEqual("1 year", age);
	}

	[TestMethod]
	public void FormatBusiestDayCombinesDateAndCount() =>
			Assert.AreEqual("2024-03-14 (1,337)", OverviewFields.FormatBusiestDay("2024-03-14", 1337));

	[TestMethod]
	public void FormatBusiestDayFallsBackWithoutACalendar() =>
			Assert.AreEqual("none", OverviewFields.FormatBusiestDay(null, 0));

	/// <summary>
	/// The same regression guard as <c>SvgTemplateTests.FormatNumberIgnoresCurrentCulture</c>:
	/// this repository is maintained on a de-DE machine, where a culture-sensitive
	/// format would emit "1,5 MB" and "März 2013" and silently rewrite every
	/// generated SVG.
	/// </summary>
	[TestMethod]
	public void FormattersIgnoreCurrentCulture()
	{
		CultureInfo original = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = new CultureInfo("de-DE");

			Assert.AreEqual("1.5 MB", OverviewFields.FormatDiskUsage(1536));
			Assert.AreEqual("2.5 GB", OverviewFields.FormatDiskUsage(2621440));
			Assert.AreEqual("March 2013", OverviewFields.FormatMemberSince("2013-03-04T10:00:00Z"));
			Assert.AreEqual("1,234 days", OverviewFields.FormatDays(1234));
			Assert.AreEqual("2024-03-14 (1,337)", OverviewFields.FormatBusiestDay("2024-03-14", 1337));
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
		}
	}
}
