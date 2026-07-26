namespace BB84.GitHub.Statistics.GitHub;

/// <summary>The GraphQL queries this application issues.</summary>
internal static class GraphQlQueries
{
	public const string BasicInfo =
			"""
        query {
          viewer {
            login
            name
            contributionsCollection {
              contributionYears
            }
          }
        }
        """;

	/// <summary>
	/// Profile-level counters for the viewer.
	/// </summary>
	/// <remarks>
	/// Deliberately separate from <see cref="BasicInfo"/>, whose failure is fatal.
	/// Every field here is a cheap <c>totalCount</c>, but <c>sponsors</c> and
	/// <c>totalDiskUsage</c> can fail on a narrowly scoped token, and losing a
	/// follower count must not take down the whole run.
	/// </remarks>
	public const string ProfileInfo =
			"""
      query {
        viewer {
          createdAt
          followers { totalCount }
          following { totalCount }
          starredRepositories { totalCount }
          gists { totalCount }
          organizations { totalCount }
          watching { totalCount }
          sponsors { totalCount }
          mergedPullRequests: pullRequests(states: MERGED) { totalCount }
          repositories(ownerAffiliations: OWNER) {
            totalCount
            totalDiskUsage
          }
        }
      }
      """;

	/// <summary>
	/// The daily contribution calendar for a date range, which is what streaks are
	/// computed from.
	/// </summary>
	/// <remarks>
	/// GitHub caps <c>contributionCalendar</c> at one year, which is why callers
	/// issue this once per contribution year. Kept apart from
	/// <see cref="ContributionTotals"/> so that the calendar and the aggregates
	/// fail independently.
	/// </remarks>
	public const string ContributionCalendar =
			"""
      query ($from: DateTime, $to: DateTime) {
        viewer {
          contributionsCollection(from: $from, to: $to) {
            restrictedContributionsCount
            contributionCalendar {
              totalContributions
              weeks {
                contributionDays {
                  date
                  contributionCount
                }
              }
            }
          }
        }
      }
      """;

	/// <summary>
	/// The five contribution totals for a date range.
	/// </summary>
	/// <remarks>
	/// Deliberately separate from <see cref="CommitContributionsByRange"/>. These
	/// aggregates are what GitHub's per-query compute budget trips over — a
	/// <c>RESOURCE_LIMITS_EXCEEDED</c> error names one of them in its path — and
	/// keeping them apart means they are computed once per year instead of once
	/// per subdivided range.
	/// </remarks>
	public const string ContributionTotals =
			"""
        query ($from: DateTime, $to: DateTime) {
          viewer {
            contributionsCollection(from: $from, to: $to) {
              totalRepositoryContributions
              totalIssueContributions
              totalCommitContributions
              totalPullRequestContributions
              totalPullRequestReviewContributions
            }
          }
        }
        """;

	/// <summary>
	/// Per-repository commit contributions for a date range.
	/// <c>maxRepositories</c> is capped at 100 by GitHub, which is why callers
	/// subdivide the range when the result saturates.
	/// </summary>
	public const string CommitContributionsByRange =
			"""
        query ($from: DateTime, $to: DateTime) {
          viewer {
            contributionsCollection(from: $from, to: $to) {
              commitContributionsByRepository(maxRepositories: 100) {
                repository {
                  nameWithOwner
                  stargazerCount
                  forkCount
                  isPrivate
                  languages(
                      first: 100,
                      orderBy: { direction: DESC, field: SIZE }
                  ) {
                    edges {
                      size
                      node {
                        name
                        color
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;
}
