using BB84.GitHub.Statistics.Rendering;

namespace BB84.GitHub.Statistics.Tests;

/// <summary>
/// These figures are approximations by construction: GitHub exposes only bytes
/// per language. The tests pin the arithmetic and the fallback, not the accuracy
/// of any individual divisor.
/// </summary>
[TestClass]
public sealed class LanguageLineEstimatesTests
{
	[TestMethod]
	[DataRow("C#", 3100L, 100L)]
	[DataRow("Python", 2800L, 100L)]
	[DataRow("Markdown", 5000L, 100L)]
	[DataRow("PowerShell", 2600L, 100L)]
	[DataRow("JSON", 4200L, 100L)]
	public void EstimateDividesByTheLanguageDivisor(string name, long bytes, long expected) =>
			Assert.AreEqual(expected, LanguageLineEstimates.Estimate(name, bytes));

	[TestMethod]
	public void EstimateFallsBackForUnlistedLanguages() =>
			Assert.AreEqual(
					100,
					LanguageLineEstimates.Estimate("Brainfuck", 100 * LanguageLineEstimates.DefaultBytesPerLine));

	/// <summary>Linguist names are case-exact, so the lookup is ordinal.</summary>
	[TestMethod]
	public void EstimateLookupIsCaseSensitive() =>
			Assert.AreEqual(
					LanguageLineEstimates.Estimate("Brainfuck", 3200),
					LanguageLineEstimates.Estimate("c#", 3200));

	[TestMethod]
	[DataRow(0L)]
	[DataRow(-1L)]
	public void EstimateOfNothingIsZero(long bytes) =>
			Assert.AreEqual(0, LanguageLineEstimates.Estimate("C#", bytes));

	/// <summary>A language smaller than one line rounds down rather than up.</summary>
	[TestMethod]
	public void EstimateRoundsDown() =>
			Assert.AreEqual(0, LanguageLineEstimates.Estimate("C#", 30));
}
