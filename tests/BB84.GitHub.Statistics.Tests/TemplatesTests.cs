using BB84.GitHub.Statistics.Rendering;
using System.Globalization;
using System.Xml.Linq;

namespace BB84.GitHub.Statistics.Tests;

/// <summary>
/// End-to-end cover for the embedded templates. Every other renderer test looks
/// at a fragment in isolation, so nothing else would catch a placeholder that the
/// template references but <c>Program.cs</c> never supplies, or markup that only
/// fails once it is assembled into a document.
/// </summary>
[TestClass]
public sealed class TemplatesTests
{
	private static readonly AggregatedLanguage[] Languages =
	[
		new("C#", 3_145_728, "#178600", 7),
		new("TypeScript", 1_048_576, "#3178c6", 4),
		new("Rock & Roll", 65_536, null, 1),
	];

	private static string RenderLanguages(params string[] fieldIds)
	{
		LanguagesMarkup markup = LanguagesRenderer.Render(
				Languages,
				4_259_840,
				LanguageFields.Resolve(fieldIds.Length == 0 ? LanguageFields.Default : fieldIds));

		return SvgTemplate.Fill(
				Templates.Languages,
				new Dictionary<string, string>(StringComparer.Ordinal)
				{
					["lang_list"] = markup.LangList,
					["progress"] = markup.Progress,
					["summary"] = markup.Summary,
					["height"] = markup.Height.ToString(CultureInfo.InvariantCulture),
					["inner_height"] = markup.InnerHeight.ToString(CultureInfo.InvariantCulture),
				});
	}

	[TestMethod]
	public void EmbeddedTemplatesAreLoadable()
	{
		Assert.IsNotEmpty(Templates.Overview);
		Assert.IsNotEmpty(Templates.Languages);
	}

	/// <summary>
	/// An SVG is parsed as XML, so this is the check that matters: malformed markup
	/// makes GitHub render nothing at all rather than something slightly wrong.
	/// </summary>
	[TestMethod]
	public void FilledLanguagesTemplateIsWellFormedXml()
	{
		XDocument document = XDocument.Parse(RenderLanguages());

		Assert.AreEqual("svg", document.Root!.Name.LocalName);
	}

	[TestMethod]
	public void FilledLanguagesTemplateIsWellFormedXmlForEveryField()
	{
		string svg = RenderLanguages("rank", "name", "percent", "size", "lines", "repos");

		XDocument document = XDocument.Parse(svg);

		Assert.AreEqual("svg", document.Root!.Name.LocalName);
		Assert.Contains("<span class=\"size\">", svg);
		Assert.Contains("<span class=\"lines\">", svg);
		Assert.Contains("<span class=\"repos\">", svg);
		Assert.Contains("<span class=\"rank\">", svg);
	}

	/// <summary>
	/// Program.cs hard-codes the languages value dictionary rather than supplying
	/// the whole catalogue the way the overview does, so a placeholder added to the
	/// template without a matching key throws at run time.
	/// </summary>
	[TestMethod]
	public void FilledLanguagesTemplateHasNoPlaceholdersLeft() =>
			Assert.DoesNotContain("{{", RenderLanguages());

	[TestMethod]
	public void FilledLanguagesTemplateCarriesTheComputedGeometry()
	{
		LanguagesMarkup markup = LanguagesRenderer.Render(
				Languages,
				4_259_840,
				LanguageFields.Resolve(LanguageFields.Default));

		XDocument document = XDocument.Parse(RenderLanguages());

		Assert.AreEqual(
				markup.Height.ToString(CultureInfo.InvariantCulture),
				document.Root!.Attribute("height")!.Value);

		XElement foreignObject = document.Descendants()
				.First(e => e.Name.LocalName == "foreignObject");

		Assert.AreEqual(
				markup.InnerHeight.ToString(CultureInfo.InvariantCulture),
				foreignObject.Attribute("height")!.Value);
	}

	[TestMethod]
	public void FilledLanguagesTemplateShowsTheSummary() =>
			Assert.Contains("<div class=\"summary\">3 languages · 4.1 MB ·", RenderLanguages());

	[TestMethod]
	public void FilledOverviewTemplateIsWellFormedXml()
	{
		AggregateStats stats = new(
				"Ada & Co",
				100, 10, 5, 1000, 50, 3, 0,
				[],
				new ContributionBreakdown(1, 2, 3, 4, 5),
				new ProfileStats(1, 2, 3, 4, 5, 6, 7, 8, 9, 2048, "2013-03-04T10:00:00Z"),
				new StreakStats(1, 2, "2024-03-14", 3, 4, 5));

		OverviewMarkup overview = OverviewRenderer.Render(
				OverviewFields.Resolve(OverviewFields.Default),
				stats);

		Dictionary<string, string> values = new(StringComparer.Ordinal)
		{
			["name"] = SvgTemplate.Escape(stats.Name),
			["rows"] = overview.Rows,
			["height"] = overview.Height.ToString(CultureInfo.InvariantCulture),
			["inner_height"] = overview.InnerHeight.ToString(CultureInfo.InvariantCulture),
		};

		foreach (OverviewField field in OverviewFields.All)
		{
			values[field.Id] = SvgTemplate.Escape(field.Value(stats));
		}

		XDocument document = XDocument.Parse(SvgTemplate.Fill(Templates.Overview, values));

		Assert.AreEqual("svg", document.Root!.Name.LocalName);
	}
}
