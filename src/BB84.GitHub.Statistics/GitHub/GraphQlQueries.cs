namespace BB84.GitHub.Statistics.GitHub;

/// <summary>The two GraphQL queries this application issues, carried over verbatim.</summary>
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
	/// Contribution totals and per-repository commit contributions for a date range.
	/// <c>maxRepositories</c> is capped at 100 by GitHub, which is why callers
	/// subdivide the range when the result saturates.
	/// </summary>
	public const string ContributionsByRange =
			"""
        query ($from: DateTime, $to: DateTime) {
          viewer {
            contributionsCollection(from: $from, to: $to) {
              totalRepositoryContributions
              totalIssueContributions
              totalCommitContributions
              totalPullRequestContributions
              totalPullRequestReviewContributions
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
