using BB84.GitHub.Statistics.Rendering;
using System.Globalization;

namespace BB84.GitHub.Statistics.Tests;

[TestClass]
public sealed class LanguageFieldsTests
{
	[TestMethod]
	public void ResolveKeepsTheRequestedOrder()
	{
		IReadOnlyList<LanguageField> fields = LanguageFields.Resolve(["repos", "name", "percent"]);

		Assert.HasCount(3, fields);
		Assert.AreEqual("repos", fields[0].Id);
		Assert.AreEqual("name", fields[1].Id);
		Assert.AreEqual("percent", fields[2].Id);
	}

	[TestMethod]
	public void ResolveIsCaseInsensitive() =>
			Assert.AreEqual("percent", LanguageFields.Resolve(["PerCent"])[0].Id);

	[TestMethod]
	public void ResolveUnknownFieldThrows()
	{
		InvalidFieldException e = Assert.ThrowsExactly<InvalidFieldException>(
				() => LanguageFields.Resolve(["percent", "nope"]));

		Assert.AreEqual("nope", e.Field);
		Assert.Contains("Valid fields:", e.Message);
	}

	/// <summary>A duplicate is a typo far more often than intent.</summary>
	[TestMethod]
	public void ResolveDuplicateFieldThrows()
	{
		InvalidFieldException e = Assert.ThrowsExactly<InvalidFieldException>(
				() => LanguageFields.Resolve(["percent", "name", "percent"]));

		Assert.AreEqual("percent", e.Field);
		Assert.Contains("more than once", e.Message);
	}

	[TestMethod]
	public void ResolveEmptyRequestYieldsNoFields() =>
			Assert.IsEmpty(LanguageFields.Resolve([]));

	/// <summary>
	/// The default set is what keeps the emitted legend byte-identical to the SVGs
	/// already on the <c>generated</c> branch.
	/// </summary>
	[TestMethod]
	public void DefaultSetIsNameThenPercent() =>
			Assert.AreEqual("name, percent", string.Join(", ", LanguageFields.Default));

	[TestMethod]
	public void EveryDefaultIdIsInTheCatalogue() =>
			Assert.HasCount(LanguageFields.Default.Count, LanguageFields.Resolve(LanguageFields.Default));

	[TestMethod]
	public void EveryCatalogueEntryHasAnIdAndCssClass()
	{
		foreach (LanguageField field in LanguageFields.All)
		{
			Assert.IsNotEmpty(field.Id, "field id");
			Assert.IsNotEmpty(field.CssClass, $"{field.Id} css class");
		}
	}

	// -- Formatters -------------------------------------------------------------

	[TestMethod]
	[DataRow(0L, "0 B")]
	[DataRow(1L, "1 B")]
	[DataRow(1023L, "1,023 B")]
	[DataRow(1024L, "1 KB")]
	[DataRow(1536L, "1.5 KB")]
	[DataRow(786432L, "768 KB")]
	[DataRow(1048576L, "1 MB")]
	[DataRow(2097152L, "2 MB")]
	[DataRow(4259840L, "4.1 MB")]
	[DataRow(1073741824L, "1 GB")]
	public void FormatBytesScalesToTheLargestFittingUnit(long bytes, string expected) =>
			Assert.AreEqual(expected, LanguageFields.FormatBytes(bytes));

	[TestMethod]
	[DataRow(0L, "0")]
	[DataRow(999L, "999")]
	[DataRow(1000L, "1.0k")]
	[DataRow(1234L, "1.2k")]
	[DataRow(9999L, "10.0k")]
	[DataRow(10000L, "10k")]
	[DataRow(41234L, "41k")]
	[DataRow(999999L, "999k")]
	[DataRow(1000000L, "1.0M")]
	[DataRow(2500000L, "2.5M")]
	public void FormatCompactAbbreviatesLargeCounts(long value, string expected) =>
			Assert.AreEqual(expected, LanguageFields.FormatCompact(value));

	/// <summary>
	/// Regression guard, matching <see cref="SvgTemplateTests.FormatNumberIgnoresCurrentCulture"/>:
	/// this repository is maintained on a de-DE machine, where a culture-sensitive
	/// format would emit <c>1,5 KB</c> and silently rewrite every generated SVG.
	/// </summary>
	[TestMethod]
	public void FormattersIgnoreCurrentCulture()
	{
		CultureInfo original = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = new CultureInfo("de-DE");

			Assert.AreEqual("1.5 KB", LanguageFields.FormatBytes(1536));
			Assert.AreEqual("1.2k", LanguageFields.FormatCompact(1234));
			Assert.AreEqual("57.06", LanguageFields.Fixed(57.058, 2));
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
		}
	}

	[TestMethod]
	public void PercentGuardsTheEmptyTotal() =>
			Assert.AreEqual(0.0, LanguageFields.Percent(new LanguageRow(new AggregatedLanguage("C#", 5, null), 0, 0)));
}
