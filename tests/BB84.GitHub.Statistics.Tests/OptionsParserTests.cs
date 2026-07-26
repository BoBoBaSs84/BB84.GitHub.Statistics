using BB84.GitHub.Statistics.Configuration;

namespace BB84.GitHub.Statistics.Tests;

[TestClass]
public sealed class OptionsParserTests
{
	private static ParseResult Parse(string[] args, Dictionary<string, string>? env = null) =>
			OptionsParser.Parse(args, env ?? [], TextWriter.Null, TextWriter.Null);

	[TestMethod]
	public void ParseReadsValueAndFlagArguments()
	{
		ParseResult result = Parse(["--access-token", "abc", "--debug"]);

		Assert.AreEqual(ParseOutcome.Success, result.Outcome);
		Assert.AreEqual("abc", result.Options.AccessToken);
		Assert.IsTrue(result.Options.Debug);
	}

	[TestMethod]
	public void ParseAcceptsUnderscoresAndMixedCase()
	{
		ParseResult result = Parse(["--Access_Token", "abc"]);

		Assert.AreEqual(ParseOutcome.Success, result.Outcome);
		Assert.AreEqual("abc", result.Options.AccessToken);
	}

	[TestMethod]
	public void ParseCommandLineBeatsEnvironment()
	{
		ParseResult result = Parse(
				["--access-token", "from-cli"],
				new Dictionary<string, string> { ["ACCESS_TOKEN"] = "from-env" });

		Assert.AreEqual("from-cli", result.Options.AccessToken);
	}

	[TestMethod]
	public void ParseEnvironmentBeatsDefault()
	{
		ParseResult result = Parse(
				[],
				new Dictionary<string, string> { ["ACCESS_TOKEN"] = "from-env", ["MAX_RETRIES"] = "3" });

		Assert.AreEqual("from-env", result.Options.AccessToken);
		Assert.AreEqual(3, result.Options.MaxRetries);
	}

	[TestMethod]
	public void ParseDefaultMaxRetriesIs25() =>
			Assert.AreEqual(25, Parse(["--access-token", "t"]).Options.MaxRetries);

	[TestMethod]
	public void ParseUnrelatedEnvironmentVariablesAreIgnored()
	{
		ParseResult result = Parse(
				["--access-token", "t"],
				new Dictionary<string, string> { ["PATH"] = "/usr/bin", ["HOME"] = "/root" });

		Assert.AreEqual(ParseOutcome.Success, result.Outcome);
	}

	/// <summary>Any non-empty value except "false" enables a flag from the environment.</summary>
	[TestMethod]
	[DataRow("true", true)]
	[DataRow("1", true)]
	[DataRow("yes", true)]
	[DataRow("false", false)]
	[DataRow("FALSE", false)]
	[DataRow("", false)]
	public void ParseEnvironmentFlagSemantics(string value, bool expected)
	{
		ParseResult result = Parse(
				[],
				new Dictionary<string, string> { ["ACCESS_TOKEN"] = "t", ["SILENT"] = value });

		Assert.AreEqual(expected, result.Options.Silent);
	}

	[TestMethod]
	public void ParseHelpIsRequestedByBothForms()
	{
		Assert.AreEqual(ParseOutcome.HelpRequested, Parse(["-h"]).Outcome);
		Assert.AreEqual(ParseOutcome.HelpRequested, Parse(["--help"]).Outcome);
	}

	[TestMethod]
	public void ParseUnknownArgumentIsAnError() =>
			Assert.AreEqual(ParseOutcome.Error, Parse(["--not-a-real-flag", "x"]).Outcome);

	[TestMethod]
	public void ParsePositionalArgumentIsAnError() =>
			Assert.AreEqual(ParseOutcome.Error, Parse(["bare-word"]).Outcome);

	[TestMethod]
	public void ParseMissingValueIsAnError() =>
			Assert.AreEqual(ParseOutcome.Error, Parse(["--access-token"]).Outcome);

	[TestMethod]
	public void ParseRequiresTokenOrInputFile()
	{
		Assert.AreEqual(ParseOutcome.Error, Parse([]).Outcome);
		Assert.AreEqual(ParseOutcome.Success, Parse(["--json-input-file", "stats.json"]).Outcome);
		Assert.AreEqual(ParseOutcome.Success, Parse(["--version"]).Outcome);
	}

	[TestMethod]
	public void ParseEmptyTokenIsTreatedAsAbsent() =>
			Assert.AreEqual(ParseOutcome.Error, Parse(["--access-token", ""]).Outcome);

	/// <summary>
	/// Dumping a template needs no credentials. The Zig original rejected this,
	/// which made <c>--dump-overview-template</c> unusable without a token.
	/// </summary>
	[TestMethod]
	public void ParseDumpingTemplatesNeedsNoToken()
	{
		Assert.AreEqual(ParseOutcome.Success, Parse(["--dump-overview-template", "o.svg"]).Outcome);
		Assert.AreEqual(ParseOutcome.Success, Parse(["--dump-languages-template", "l.svg"]).Outcome);
	}

	[TestMethod]
	public void ParseNonNumericMaxRetriesIsAnErrorNotACrash() =>
			Assert.AreEqual(ParseOutcome.Error, Parse(["--access-token", "t", "--max-retries", "abc"]).Outcome);

	[TestMethod]
	public void ParseNonNumericMaxRetriesFromEnvironmentIsAnError()
	{
		ParseResult result = Parse(
				[],
				new Dictionary<string, string> { ["ACCESS_TOKEN"] = "t", ["MAX_RETRIES"] = "not-a-number" });

		Assert.AreEqual(ParseOutcome.Error, result.Outcome);
	}

	[TestMethod]
	public void ParseRepeatedFlagTakesTheLastValue() =>
			Assert.AreEqual("second", Parse(["--access-token", "first", "--access-token", "second"]).Options.AccessToken);

	[TestMethod]
	public void ExcludedPatternsAreEmptyWhenUnset()
	{
		AppOptions options = new();

		Assert.IsEmpty(options.ExcludedRepoPatterns);
		Assert.IsEmpty(options.ExcludedLangPatterns);
	}

	[TestMethod]
	public void ExcludedRepoPatternsSplitOnAllSeparators()
	{
		string[] expected = ["a/one", "a/two", "a/three", "a/four"];
		AppOptions options = new() { ExcludeRepos = "a/one, a/two|a/three\ta/four" };

		Assert.AreSequenceEqual(expected, [.. options.ExcludedRepoPatterns]);
	}

	[TestMethod]
	public void ExcludedLangPatternsPreserveInternalSpaces()
	{
		string[] expected = ["Jupyter Notebook", "C#"];
		AppOptions options = new() { ExcludeLangs = "Jupyter Notebook, C#" };

		Assert.AreSequenceEqual(expected, [.. options.ExcludedLangPatterns]);
	}

	[TestMethod]
	public void ParseReadsOverviewFields()
	{
		ParseResult result = Parse(["--access-token", "abc", "--overview-fields", "stars,followers"]);

		Assert.AreEqual(ParseOutcome.Success, result.Outcome);
		Assert.AreEqual("stars,followers", result.Options.OverviewFields);
	}

	[TestMethod]
	public void ParseReadsOverviewFieldsFromEnvironment()
	{
		ParseResult result = Parse(
				[],
				new Dictionary<string, string>
				{
					["ACCESS_TOKEN"] = "abc",
					["OVERVIEW_FIELDS"] = "stars,forks",
				});

		Assert.AreEqual("stars,forks", result.Options.OverviewFields);
	}

	[TestMethod]
	public void ParseOverviewFieldsFromCommandLineBeatsEnvironment()
	{
		ParseResult result = Parse(
				["--access-token", "abc", "--overview-fields", "from-cli"],
				new Dictionary<string, string> { ["OVERVIEW_FIELDS"] = "from-env" });

		Assert.AreEqual("from-cli", result.Options.OverviewFields);
	}

	/// <summary>Absent means "render the default rows", which Program.cs decides, not the parser.</summary>
	[TestMethod]
	public void OverviewFieldsAreUnsetByDefault()
	{
		AppOptions options = new();

		Assert.IsNull(options.OverviewFields);
		Assert.IsEmpty(options.OverviewFieldIds);
	}

	/// <summary>
	/// Split like <c>--exclude-repos</c> rather than <c>--exclude-langs</c>: no
	/// field id contains a space, so a stray one should not create an empty entry.
	/// </summary>
	[TestMethod]
	public void OverviewFieldIdsSplitOnCommasAndSpaces()
	{
		string[] expected = ["stars", "forks", "followers", "commits"];
		AppOptions options = new() { OverviewFields = "stars, forks followers|commits" };

		Assert.AreSequenceEqual(expected, [.. options.OverviewFieldIds]);
	}
}
