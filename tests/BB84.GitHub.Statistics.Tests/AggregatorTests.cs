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

	/// <summary>
	/// The overview can show the five contribution totals individually, so they
	/// have to survive aggregation rather than only reaching it as their sum.
	/// </summary>
	[TestMethod]
	public void AggregateSplitsOutTheContributionBreakdown()
	{
		StatisticsDocument stats = new()
		{
			CommitContributions = 10,
			PrContributions = 5,
			ReviewContributions = 4,
			IssueContributions = 3,
			RepoContributions = 2,
		};

		AggregateStats result = Aggregator.Aggregate(stats, new AppOptions());

		Assert.AreEqual(10, result.Breakdown.Commits);
		Assert.AreEqual(5, result.Breakdown.Prs);
		Assert.AreEqual(4, result.Breakdown.Reviews);
		Assert.AreEqual(3, result.Breakdown.Issues);
		Assert.AreEqual(2, result.Breakdown.ReposCreated);
		Assert.AreEqual(24, result.Contributions, "the combined total still adds up");
	}

	[TestMethod]
	public void AggregateCarriesProfileAndStreakValues()
	{
		StatisticsDocument stats = new()
		{
			CreatedAt = "2013-03-04T10:00:00Z",
			Followers = 321,
			Following = 21,
			StarsGiven = 400,
			Gists = 3,
			Organizations = 2,
			Watching = 44,
			Sponsors = 1,
			MergedPullRequests = 55,
			OwnRepositories = 66,
			DiskUsageKb = 2048,
			CurrentStreak = 5,
			LongestStreak = 88,
			BusiestDay = "2024-03-14",
			BusiestDayCount = 37,
			ContributionsThisYear = 1500,
			PrivateContributions = 250,
		};

		AggregateStats result = Aggregator.Aggregate(stats, new AppOptions());

		Assert.AreEqual("2013-03-04T10:00:00Z", result.Profile.CreatedAt);
		Assert.AreEqual(321, result.Profile.Followers);
		Assert.AreEqual(66, result.Profile.OwnRepositories);
		Assert.AreEqual(2048, result.Profile.DiskUsageKb);
		Assert.AreEqual(5, result.Streaks.Current);
		Assert.AreEqual(88, result.Streaks.Longest);
		Assert.AreEqual("2024-03-14", result.Streaks.BusiestDay);
		Assert.AreEqual(37, result.Streaks.BusiestDayCount);
		Assert.AreEqual(1500, result.Streaks.ThisYear);
		Assert.AreEqual(250, result.Streaks.Private);
	}

	/// <summary>
	/// The profile, breakdown and streak figures are user-level, not
	/// per-repository, so the repository filters must leave them alone. This is why
	/// <c>own_repos</c> and <c>repos</c> can legitimately disagree.
	/// </summary>
	[TestMethod]
	public void AggregateDoesNotApplyRepositoryFiltersToUserLevelValues()
	{
		StatisticsDocument stats = new()
		{
			CommitContributions = 10,
			Followers = 321,
			OwnRepositories = 66,
			LongestStreak = 88,
			Repositories =
				[
						Repo("jstrieb/one", stars: 3),
								Repo("a/secret", stars: 4, isPrivate: true),
						],
		};

		AppOptions options = new() { ExcludeRepos = "jstrieb/*", ExcludePrivate = true };

		AggregateStats result = Aggregator.Aggregate(stats, options);

		Assert.AreEqual(0, result.Repos, "both repositories are filtered out");
		Assert.AreEqual(0, result.Stars);
		Assert.AreEqual(10, result.Breakdown.Commits, "contribution totals are not per-repository");
		Assert.AreEqual(321, result.Profile.Followers);
		Assert.AreEqual(66, result.Profile.OwnRepositories);
		Assert.AreEqual(88, result.Streaks.Longest);
	}
}
