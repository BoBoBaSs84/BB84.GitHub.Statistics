using BB84.GitHub.Statistics.Rendering;
using System.Globalization;

namespace BB84.GitHub.Statistics.Tests;

[TestClass]
public sealed class SvgTemplateTests
{
	private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal)
	{
		["name"] = "Ada",
		["stars"] = "12",
	};

	[TestMethod]
	public void FillSubstitutesPlaceholders() =>
			Assert.AreEqual("<p>Ada has 12</p>", SvgTemplate.Fill("<p>{{ name }} has {{ stars }}</p>", Values));

	[TestMethod]
	public void FillTolerantOfPlaceholderSpacing() =>
			Assert.AreEqual("AdaAdaAda", SvgTemplate.Fill("{{name}}{{ name }}{{   name   }}", Values));

	[TestMethod]
	public void FillLeavesTemplateWithoutPlaceholdersUnchanged() =>
			Assert.AreEqual("<svg></svg>", SvgTemplate.Fill("<svg></svg>", Values));

	[TestMethod]
	public void FillUnknownFieldThrows()
	{
		InvalidFieldException e = Assert.ThrowsExactly<InvalidFieldException>(
				() => SvgTemplate.Fill("{{ nope }}", Values));

		Assert.AreEqual("nope", e.Field);
	}

	/// <summary>An unterminated <c>{{</c> is emitted literally rather than failing.</summary>
	[TestMethod]
	public void FillUnterminatedPlaceholderIsLiteral() =>
			Assert.AreEqual("a {{ name", SvgTemplate.Fill("a {{ name", Values));

	[TestMethod]
	public void FillEmptyTemplateProducesEmptyOutput() =>
			Assert.AreEqual(string.Empty, SvgTemplate.Fill(string.Empty, Values));

	/// <summary>
	/// Only placeholders a template references are checked. Program.cs relies on
	/// this: it supplies every field in the overview catalogue whether or not the
	/// template asks for them, so custom templates can use any of them.
	/// </summary>
	[TestMethod]
	public void FillIgnoresValuesTheTemplateDoesNotReference() =>
			Assert.AreEqual("Ada", SvgTemplate.Fill("{{ name }}", Values));

	[TestMethod]
	[DataRow(0L, "0")]
	[DataRow(1L, "1")]
	[DataRow(999L, "999")]
	[DataRow(1000L, "1,000")]
	[DataRow(11072L, "11,072")]
	[DataRow(14361640L, "14,361,640")]
	public void FormatNumberUsesCommaGroups(long value, string expected) =>
			Assert.AreEqual(expected, SvgTemplate.FormatNumber(value));

	/// <summary>
	/// Regression guard: this repository is maintained on a de-DE machine, where a
	/// culture-sensitive format would emit 14.361.640 and silently rewrite every
	/// generated SVG.
	/// </summary>
	[TestMethod]
	public void FormatNumberIgnoresCurrentCulture()
	{
		CultureInfo original = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = new CultureInfo("de-DE");
			Assert.AreEqual("14,361,640", SvgTemplate.FormatNumber(14361640));
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
		}
	}
}
