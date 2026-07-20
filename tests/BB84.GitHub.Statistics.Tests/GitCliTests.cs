using BB84.GitHub.Statistics.Statistics;

namespace BB84.GitHub.Statistics.Tests;

[TestClass]
public sealed class GitCliTests
{
	[TestMethod]
	public void SumNumstatAddsAdditionsAndDeletions() =>
			Assert.AreEqual(33u, GitCli.SumNumstat("10\t20\tsrc/a.cs\n2\t1\tsrc/b.cs\n"));

	/// <summary>Binary files report "-" for both counts, which must not throw.</summary>
	[TestMethod]
	public void SumNumstatTreatsBinaryMarkersAsZero() =>
			Assert.AreEqual(5u, GitCli.SumNumstat("-\t-\timage.png\n3\t2\tsrc/a.cs\n"));

	[TestMethod]
	public void SumNumstatIgnoresBlankLines() =>
			Assert.AreEqual(3u, GitCli.SumNumstat("\n\n1\t2\tfile\n\n"));

	[TestMethod]
	public void SumNumstatHandlesEmptyInput() =>
			Assert.AreEqual(0u, GitCli.SumNumstat(string.Empty));

	[TestMethod]
	public void SumNumstatAcceptsSpaceSeparatedFields() =>
			Assert.AreEqual(7u, GitCli.SumNumstat("4 3 path/to/file\n"));

	/// <summary>Paths containing spaces must not be parsed as counts.</summary>
	[TestMethod]
	public void SumNumstatIgnoresPathContents() =>
			Assert.AreEqual(3u, GitCli.SumNumstat("1\t2\tsome dir/with 99 spaces.cs\n"));

	[TestMethod]
	public void SumNumstatSkipsMalformedLines() =>
			Assert.AreEqual(3u, GitCli.SumNumstat("garbage\n1\t2\tfile\n"));

	[TestMethod]
	public void SumNumstatHandlesCrlfLineEndings() =>
			Assert.AreEqual(3u, GitCli.SumNumstat("1\t2\tfile\r\n"));
}
