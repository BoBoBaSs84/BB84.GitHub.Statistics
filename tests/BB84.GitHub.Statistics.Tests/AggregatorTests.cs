using BB84.GitHub.Statistics.Configuration;
using BB84.GitHub.Statistics.Statistics;

namespace BB84.GitHub.Statistics.Tests;

[TestClass]
public sealed class AggregatorTests
{
	private static RepositoryStats Repo(
			string name,
			uint stars = 0,
			uint forks = 0,
			uint lines = 0,
			uint views = 0,
			bool isPrivate = false,
			List<LanguageStats>? languages = null) =>
			new()
			{
				Name = name,
				Stars = stars,
				Forks = forks,
				LinesChanged = lines,
				Views = views,
				Private = isPrivate,
				Languages = languages,
			};

	private static LanguageStats Lang(string name, uint size, string? color = "#fff") =>
			new() { Name = name, Size = size, Color = color };

	[TestMethod]
	public void AggregateSumsTotalsAcrossRepositories()
	{
		StatisticsDocument stats = new()
		{
			Name = "Ada",
			CommitContributions = 10,
			PrContributions = 5,
			Repositories =
				[
						Repo("a/one", stars: 3, forks: 1, lines: 100, views: 7),
								Repo("a/two", stars: 4, forks: 2, lines: 200, views: 8),
						],
		};

		AggregateStats result = Aggregator.Aggregate(stats, new AppOptions());

		Assert.AreEqual(7, result.Stars);
		Assert.AreEqual(3, result.Forks);
		Assert.AreEqual(300, result.LinesChanged);
		Assert.AreEqual(15, result.Views);
		Assert.AreEqual(2, result.Repos);
		Assert.AreEqual(15, result.Contributions);
		Assert.AreEqual("Ada", result.Name);
	}

	[TestMethod]
	public void AggregateExcludesReposByGlob()
	{
		StatisticsDocument stats = new()
		{
			Repositories =
				[
						Repo("jstrieb/github-stats", stars: 10),
								Repo("jstrieb/other", stars: 20),
								Repo("someone/keep", stars: 5),
						],
		};

		AggregateStats result = Aggregator.Aggregate(stats, new AppOptions { ExcludeRepos = "jstrieb/*" });

		Assert.AreEqual(1, result.Repos);
		Assert.AreEqual(5, result.Stars);
	}

	[TestMethod]
	public void AggregateExcludesPrivateReposOnlyWhenRequested()
	{
		StatisticsDocument stats = new()
		{
			Repositories = [Repo("a/pub", stars: 1), Repo("a/sec", stars: 2, isPrivate: true)],
		};

		Assert.AreEqual(3, Aggregator.Aggregate(stats, new AppOptions()).Stars);
		Assert.AreEqual(1, Aggregator.Aggregate(stats, new AppOptions { ExcludePrivate = true }).Stars);
	}

	[TestMethod]
	public void AggregateMergesAndSortsLanguagesDescending()
	{
		StatisticsDocument stats = new()
		{
			Repositories =
				[
						Repo("a/one", languages: [Lang("C#", 100), Lang("Zig", 500)]),
								Repo("a/two", languages: [Lang("C#", 900)]),
						],
		};

		AggregateStats result = Aggregator.Aggregate(stats, new AppOptions());

		Assert.HasCount(2, result.Languages);
		Assert.AreEqual("C#", result.Languages[0].Name);
		Assert.AreEqual(1000, result.Languages[0].Size);
		Assert.AreEqual("Zig", result.Languages[1].Name);
		Assert.AreEqual(1500, result.LanguagesTotal);
	}

	[TestMethod]
	public void AggregateExcludedLanguagesDoNotCountTowardTotal()
	{
		StatisticsDocument stats = new()
		{
			Repositories = [Repo("a/one", languages: [Lang("C#", 100), Lang("HTML", 900)])],
		};

		AggregateStats result = Aggregator.Aggregate(stats, new AppOptions { ExcludeLangs = "html" });

		Assert.HasCount(1, result.Languages);
		Assert.AreEqual(100, result.LanguagesTotal);
	}

	/// <summary>Language names may contain spaces, so only commas split that list.</summary>
	[TestMethod]
	public void AggregateLanguageExclusionKeepsMultiWordNamesIntact()
	{
		StatisticsDocument stats = new()
		{
			Repositories = [Repo("a/one", languages: [Lang("Jupyter Notebook", 100), Lang("C#", 50)])],
		};

		AggregateStats result = Aggregator.Aggregate(stats, new AppOptions { ExcludeLangs = "Jupyter Notebook" });

		Assert.HasCount(1, result.Languages);
		Assert.AreEqual("C#", result.Languages[0].Name);
	}

	/// <summary>Repository names never contain spaces, so that list splits on them too.</summary>
	[TestMethod]
	public void AggregateRepoExclusionSplitsOnSpacesAndCommas()
	{
		StatisticsDocument stats = new()
		{
			Repositories = [Repo("a/one", stars: 1), Repo("a/two", stars: 2), Repo("a/three", stars: 4)],
		};

		AggregateStats result = Aggregator.Aggregate(stats, new AppOptions { ExcludeRepos = "a/one a/two" });

		Assert.AreEqual(1, result.Repos);
		Assert.AreEqual(4, result.Stars);
	}

	[TestMethod]
	public void AggregateNullLanguageListIsIgnored()
	{
		StatisticsDocument stats = new() { Repositories = [Repo("a/one", stars: 1, languages: null)] };

		AggregateStats result = Aggregator.Aggregate(stats, new AppOptions());

		Assert.IsEmpty(result.Languages);
		Assert.AreEqual(0, result.LanguagesTotal);
		Assert.AreEqual(1, result.Repos);
	}

	[TestMethod]
	public void AggregateLastNonNullColourWins()
	{
		StatisticsDocument stats = new()
		{
			Repositories =
				[
						Repo("a/one", languages: [Lang("C#", 1, "#178600")]),
								Repo("a/two", languages: [Lang("C#", 1, null)]),
						],
		};

		AggregateStats result = Aggregator.Aggregate(stats, new AppOptions());

		Assert.AreEqual("#178600", result.Languages[0].Color);
	}
}
