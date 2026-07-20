using BB84.GitHub.Statistics.Matching;

namespace BB84.GitHub.Statistics.Tests;

/// <summary>
/// Ported verbatim from the Zig implementation's <c>glob.zig</c> test block so the
/// reimplementation cannot silently change which repositories or languages users
/// have excluded.
/// </summary>
[TestClass]
public sealed class GlobTests
{
	[TestMethod]
	[DataRow("", "")]
	[DataRow("*", "")]
	[DataRow("**", "")]
	[DataRow("***", "")]
	[DataRow("*", "a")]
	[DataRow("**", "a")]
	[DataRow("***", "a")]
	[DataRow("*", "abcd")]
	[DataRow("**", "abcd")]
	[DataRow("****", "abcd")]
	[DataRow("****d", "abcd")]
	[DataRow("a****", "abcd")]
	[DataRow("a****d", "abcd")]
	[DataRow("abc", "abc")]
	[DataRow("*abc", "dabc")]
	[DataRow("abc*", "abcd")]
	[DataRow("*abc*", "abc")]
	[DataRow("*abc*", "dabc")]
	[DataRow("*abc*", "abcd")]
	[DataRow("*abc*", "dabcd")]
	[DataRow("*e*", "this is a test")]
	[DataRow("som*thing", "something")]
	[DataRow("som*thing", "someeeething")]
	[DataRow("som*thing", "som thing")]
	[DataRow("som*thing", "somabcthing")]
	[DataRow("som*thing", "somthing")]
	public void MatchMatches(string pattern, string value) =>
			Assert.IsTrue(Glob.Match(pattern, value), $"expected '{pattern}' to match '{value}'");

	[TestMethod]
	[DataRow("****c", "abcd")]
	[DataRow("abc", "abcd")]
	[DataRow("abc", "dabc")]
	[DataRow("abc", "dabcd")]
	[DataRow("*abc", "dabcd")]
	[DataRow("abc*", "dabcd")]
	[DataRow("*c*", "this is a test")]
	public void MatchDoesNotMatch(string pattern, string value) =>
			Assert.IsFalse(Glob.Match(pattern, value), $"expected '{pattern}' not to match '{value}'");

	[TestMethod]
	public void MatchHandlesManyStars()
	{
		string haystack = new string('s', 10) + "a" + new string('s', 10);

		Assert.IsTrue(Glob.Match("s*a" + string.Concat(Enumerable.Repeat("*s", 8)), haystack));
		Assert.IsTrue(Glob.Match("s" + string.Concat(Enumerable.Repeat("*s", 8)), haystack));
		Assert.IsTrue(Glob.Match(string.Concat(Enumerable.Repeat("s*", 8)) + "a*s", haystack));
	}

	[TestMethod]
	public void MatchExponentialWorstCaseStillTerminates() =>
			Assert.IsFalse(Glob.Match(string.Concat(Enumerable.Repeat("s*", 8)) + "a", new string('s', 30)));

	[TestMethod]
	[DataRow("*", "///")]
	[DataRow("*", "/asdf//")]
	[DataRow("/*sdf/*/*", "/asdf//")]
	[DataRow("/*sdf/*", "/asdf//")]
	public void MatchStarSpansSlashes(string pattern, string value) =>
			Assert.IsTrue(Glob.Match(pattern, value), "globbing here does not separate on slashes like the shell");

	[TestMethod]
	public void MatchIsCaseInsensitive()
	{
		Assert.IsTrue(Glob.Match("JSTRIEB/*", "jstrieb/github-stats"));
		Assert.IsTrue(Glob.Match("jstrieb/*", "JSTRIEB/GitHub-Stats"));
	}

	/// <summary>
	/// <c>?</c> is a literal here, unlike shell globbing or
	/// <c>FileSystemName.MatchesSimpleExpression</c>.
	/// </summary>
	[TestMethod]
	public void MatchQuestionMarkIsLiteral()
	{
		Assert.IsFalse(Glob.Match("ab?", "abc"));
		Assert.IsTrue(Glob.Match("ab?", "ab?"));
	}

	[TestMethod]
	public void MatchAnyWorks()
	{
		Assert.IsTrue(Glob.MatchAny(["*waw", "wew*", "wow", "www"], "wow"));
		Assert.IsFalse(Glob.MatchAny(["*waw", "wew*", "www"], "wow"));
		Assert.IsTrue(Glob.MatchAny(["w*w", "www"], "wow"));
	}

	[TestMethod]
	public void MatchAnyEmptyPatternListMatchesNothing() =>
			Assert.IsFalse(Glob.MatchAny([], "anything"));
}
