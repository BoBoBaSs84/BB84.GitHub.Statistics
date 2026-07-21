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
