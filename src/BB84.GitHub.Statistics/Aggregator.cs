using BB84.GitHub.Statistics.Configuration;
using BB84.GitHub.Statistics.Matching;
using BB84.GitHub.Statistics.Rendering;
using BB84.GitHub.Statistics.Statistics;

namespace BB84.GitHub.Statistics;

/// <summary>
/// The five contribution totals the collector already gathers, split out so the
/// overview can show them individually instead of only their sum.
/// </summary>
internal sealed record ContributionBreakdown(
		long Commits,
		long Prs,
		long Reviews,
		long Issues,
		long ReposCreated);

/// <summary>Profile-level counters, none of which are per-repository.</summary>
internal sealed record ProfileStats(
		long Followers,
		long Following,
		long StarsGiven,
		long Gists,
		long Organizations,
		long Watching,
		long Sponsors,
		long MergedPullRequests,
		long OwnRepositories,
		long DiskUsageKb,
		string? CreatedAt);

/// <summary>Figures derived from the daily contribution calendar.</summary>
internal sealed record StreakStats(
		long Current,
		long Longest,
		string? BusiestDay,
		long BusiestDayCount,
		long ThisYear,
		long Private);

/// <summary>Per-user totals rolled up from the individual repositories.</summary>
internal sealed record AggregateStats(
		string Name,
		long Contributions,
		long Stars,
		long Forks,
		long LinesChanged,
		long Views,
		long Repos,
		long LanguagesTotal,
		IReadOnlyList<AggregatedLanguage> Languages,
		ContributionBreakdown Breakdown,
		ProfileStats Profile,
		StreakStats Streaks);

/// <summary>Applies the exclusion filters and rolls repositories up into totals.</summary>
internal static class Aggregator
{
	public static AggregateStats Aggregate(StatisticsDocument statistics, AppOptions options)
	{
		IReadOnlyList<string> excludeRepos = options.ExcludedRepoPatterns;
		IReadOnlyList<string> excludeLangs = options.ExcludedLangPatterns;

		long stars = 0, forks = 0, linesChanged = 0, views = 0, repos = 0, languagesTotal = 0;

		// Insertion-ordered so that languages with identical totals keep a stable
		// first-seen order after sorting.
		Dictionary<string, long> languageSizes = new(StringComparer.Ordinal);
		Dictionary<string, string?> languageColors = new(StringComparer.Ordinal);
		List<string> languageOrder = [];

		foreach (RepositoryStats repository in statistics.Repositories)
		{
			if (Glob.MatchAny(excludeRepos, repository.Name) ||
					(options.ExcludePrivate && repository.Private))
			{
				continue;
			}

			stars += repository.Stars;
			forks += repository.Forks;
			linesChanged += repository.LinesChanged;
			views += repository.Views;
			repos++;

			if (repository.Languages is null)
			{
				continue;
			}

			foreach (LanguageStats language in repository.Languages)
			{
				if (Glob.MatchAny(excludeLangs, language.Name))
				{
					continue;
				}

				if (language.Color is not null)
				{
					languageColors[language.Name] = language.Color;
				}

				if (!languageSizes.TryGetValue(language.Name, out long total))
				{
					languageOrder.Add(language.Name);
				}

				languageSizes[language.Name] = total + language.Size;
				languagesTotal += language.Size;
			}
		}

		List<AggregatedLanguage> languages = [.. languageOrder
						.Select(name => new AggregatedLanguage(
								name,
								languageSizes[name],
								languageColors.GetValueOrDefault(name)))
						.OrderByDescending(static l => l.Size)];

		return new AggregateStats(
				statistics.Name,
				statistics.TotalContributions,
				stars,
				forks,
				linesChanged,
				views,
				repos,
				languagesTotal,
				languages,
				// The three groups below are user-level rather than per-repository,
				// so --exclude-repos and --exclude-private deliberately do not apply
				// to them. That is why `own_repos` and `repos` can disagree.
				new ContributionBreakdown(
						statistics.CommitContributions,
						statistics.PrContributions,
						statistics.ReviewContributions,
						statistics.IssueContributions,
						statistics.RepoContributions),
				new ProfileStats(
						statistics.Followers,
						statistics.Following,
						statistics.StarsGiven,
						statistics.Gists,
						statistics.Organizations,
						statistics.Watching,
						statistics.Sponsors,
						statistics.MergedPullRequests,
						statistics.OwnRepositories,
						statistics.DiskUsageKb,
						statistics.CreatedAt),
				new StreakStats(
						statistics.CurrentStreak,
						statistics.LongestStreak,
						statistics.BusiestDay,
						statistics.BusiestDayCount,
						statistics.ContributionsThisYear,
						statistics.PrivateContributions));
	}
}
