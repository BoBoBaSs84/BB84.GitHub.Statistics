using BB84.GitHub.Statistics.GitHub;
using BB84.GitHub.Statistics.Statistics;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text.Json;

namespace BB84.GitHub.Statistics.Tests;

/// <summary>
/// Covers how the collector reacts to GraphQL <c>RESOURCE_LIMITS_EXCEEDED</c>.
/// </summary>
/// <remarks>
/// GitHub reports that error with an HTTP 200 and partial data, so the fields it
/// gave up on read back as zero. Counting those zeros produces plausible but
/// wrong totals with no failure anywhere, which is exactly what these tests exist
/// to prevent.
/// </remarks>
[TestClass]
public sealed class StatisticsCollectorTests
{
	private const string ResourceLimitError =
			"""{ "type": "RESOURCE_LIMITS_EXCEEDED", "message": "Resource limits for this query exceeded." }""";

	/// <summary>Dispatches on the request URL, and on the query text for GraphQL posts.</summary>
	private sealed class StubHandler(Func<string, string?, (HttpStatusCode Status, string Body)> respond)
			: HttpMessageHandler
	{
		/// <summary>The <c>from</c> variable of every totals query, in order.</summary>
		public List<string> TotalsRanges { get; } = [];

		/// <summary>The <c>from</c> variable of every commit-contributions query, in order.</summary>
		public List<string> CommitRanges { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
		{
			string url = request.RequestUri!.ToString();
			string? body = request.Content is null
					? null
					: await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

			if (body is not null)
			{
				if (body.Contains("totalIssueContributions", StringComparison.Ordinal))
				{
					TotalsRanges.Add(FromVariable(body));
				}
				else if (body.Contains("commitContributionsByRepository", StringComparison.Ordinal))
				{
					CommitRanges.Add(FromVariable(body));
				}
			}

			(HttpStatusCode status, string responseBody) = respond(url, body);
			return new HttpResponseMessage(status) { Content = new StringContent(responseBody) };
		}

		private static string FromVariable(string body)
		{
			using JsonDocument document = JsonDocument.Parse(body);
			return document.RootElement.GetProperty("variables").GetProperty("from").GetString()!;
		}
	}

	private sealed class StubGit() : GitCli(NullLogger<GitCli>.Instance)
	{
		public override Task<uint> GetLinesChangedAsync(
				string login,
				string token,
				string repo,
				IReadOnlyList<string> emails,
				CancellationToken ct) =>
				Task.FromResult(0u);
	}

	// Built by concatenation rather than an interpolated raw string: the fragments
	// are wrapped in braces, and `{{{collection}}` next to a literal brace does not
	// read as one interpolation.

	/// <summary>A GraphQL body carrying <paramref name="collection"/> as the viewer's contributions.</summary>
	private static string Data(string collection) =>
			"{ \"data\": { \"viewer\": { \"contributionsCollection\": {" + collection + "} } } }";

	private static string DataWithErrors(string collection, string errors) =>
			"{ \"data\": { \"viewer\": { \"contributionsCollection\": {" + collection + "} } },"
			+ " \"errors\": [" + errors + "] }";

	private static string Repositories(params string[] names)
	{
		IEnumerable<string> entries = names.Select(static name =>
				$$"""
                {
                  "repository": {
                    "nameWithOwner": "{{name}}",
                    "stargazerCount": 1,
                    "forkCount": 0,
                    "isPrivate": false,
                    "languages": { "edges": [] }
                  }
                }
                """);

		return $$""" "commitContributionsByRepository": [ {{string.Join(",", entries)}} ] """;
	}

	/// <summary>
	/// Builds a collector whose non-GraphQL endpoints all answer successfully, so
	/// each test only has to describe the GraphQL responses it cares about.
	/// </summary>
	/// <param name="totals">Answers the contribution-totals query, given its zero-based call index.</param>
	/// <param name="commits">Answers the commit-contributions query, given its zero-based call index.</param>
	/// <param name="years">The contribution years the viewer query reports.</param>
	private static (StatisticsCollector Collector, StubHandler Handler) Build(
			Func<int, (HttpStatusCode, string)> totals,
			Func<int, (HttpStatusCode, string)> commits,
			params int[] years)
	{
		int totalsCalls = 0;
		int commitCalls = 0;

		StubHandler handler = new((url, body) =>
		{
			if (body is not null)
			{
				if (body.Contains("contributionYears", StringComparison.Ordinal))
				{
					string list = string.Join(",", years);
					return (HttpStatusCode.OK,
							$$"""
                            {
                              "data": {
                                "viewer": {
                                  "login": "ada",
                                  "name": "Ada",
                                  "contributionsCollection": { "contributionYears": [{{list}}] }
                                }
                              }
                            }
                            """);
				}

				if (body.Contains("totalIssueContributions", StringComparison.Ordinal))
				{
					return totals(totalsCalls++);
				}

				return commits(commitCalls++);
			}

			if (url.EndsWith("/user/emails", StringComparison.Ordinal))
			{
				return (HttpStatusCode.OK, """[ { "email": "ada@example.com" } ]""");
			}

			if (url.EndsWith("/traffic/views", StringComparison.Ordinal))
			{
				return (HttpStatusCode.OK, """{ "count": 7 }""");
			}

			// stats/contributors: a matching author, so the clone fallback never runs.
			return (HttpStatusCode.OK, """[ { "author": { "login": "ada" }, "weeks": [ { "a": 10, "d": 5 } ] } ]""");
		});

		GitHubApiClient client = new(new HttpClient(handler), NullLogger<GitHubApiClient>.Instance);
		LinesChangedResolver resolver = new(client, new StubGit(), NullLogger<LinesChangedResolver>.Instance)
		{
			Delay = static (_, _) => Task.CompletedTask,
			UtcNowSeconds = static () => 0,
		};

		StatisticsCollector collector = new(client, resolver, NullLogger<StatisticsCollector>.Instance)
		{
			Delay = static (_, _) => Task.CompletedTask,
		};

		return (collector, handler);
	}

	private static (HttpStatusCode, string) Ok(string body) => (HttpStatusCode.OK, body);

	/// <summary>
	/// The regression this change exists for. A resource-limited response used to
	/// deserialize its missing scalars to zero and count them; it must now narrow
	/// the range and sum the pieces instead.
	/// </summary>
	[TestMethod]
	public async Task CollectSubdividesTotalsWhenResourceLimitsAreExceeded()
	{
		// The full year is refused, having only counted five commits. Each half
		// answers in full. Counting the partial response would yield 5, not 30.
		(StatisticsCollector collector, StubHandler handler) = Build(
				totals: call => call == 0
						? Ok(DataWithErrors(""" "totalCommitContributions": 5 """, ResourceLimitError))
						: Ok(Data(""" "totalCommitContributions": 15, "totalIssueContributions": 2 """)),
				commits: _ => Ok(Data(Repositories())),
				2024);

		StatisticsDocument statistics = await collector.CollectAsync("token", maxRetries: 1, CancellationToken.None);

		Assert.AreEqual(30u, statistics.CommitContributions, "both halves must be counted, not the partial year");
		Assert.AreEqual(4u, statistics.IssueContributions);

		string[] expectedRanges = ["2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z", "2024-07-01T00:00:00Z"];
		Assert.AreSequenceEqual(
				expectedRanges, handler.TotalsRanges, "the refused year should be retried as two six-month halves");
	}

	/// <summary>
	/// Splitting the totals out of the repository walk means they are computed
	/// once per year, however far the repository query has to be narrowed.
	/// </summary>
	[TestMethod]
	public async Task CollectRequestsTotalsOncePerYearWhileRepositoriesSubdivide()
	{
		(StatisticsCollector collector, StubHandler handler) = Build(
				totals: _ => Ok(Data(""" "totalCommitContributions": 3 """)),
				commits: call => call == 0
						? Ok(DataWithErrors(Repositories(), ResourceLimitError))
						: Ok(Data(Repositories())),
				2023,
				2024);

		StatisticsDocument statistics = await collector.CollectAsync("token", maxRetries: 1, CancellationToken.None);

		Assert.AreEqual(6u, statistics.CommitContributions);
		Assert.HasCount(2, handler.TotalsRanges, "one totals query per year, regardless of repository narrowing");
		Assert.HasCount(4, handler.CommitRanges, "2023 refused once then split in two, 2024 answered directly");
	}

	/// <summary>Any error other than a resource limit is a real failure and must not be swallowed.</summary>
	[TestMethod]
	public async Task CollectThrowsOnGraphQlErrorsOtherThanResourceLimits()
	{
		(StatisticsCollector collector, _) = Build(
				totals: _ => Ok(DataWithErrors(
						string.Empty,
						"""{ "type": "FORBIDDEN", "message": "Resource not accessible by integration" }""")),
				commits: _ => Ok(Data(Repositories())),
				2024);

		HttpRequestException error = await Assert.ThrowsExactlyAsync<HttpRequestException>(
				() => collector.CollectAsync("token", maxRetries: 1, CancellationToken.None));

		Assert.IsTrue(
				error.Message.Contains("Resource not accessible by integration", StringComparison.Ordinal),
				error.Message);
	}

	/// <summary>
	/// At one month the range cannot be narrowed further. The query is retried a
	/// few times, then the partial data is accepted with a warning rather than
	/// failing the whole run.
	/// </summary>
	[TestMethod]
	public async Task CollectRetriesAtTheMonthFloorThenAcceptsPartialData()
	{
		(StatisticsCollector collector, StubHandler handler) = Build(
				totals: _ => Ok(DataWithErrors(""" "totalCommitContributions": 1 """, ResourceLimitError)),
				commits: _ => Ok(Data(Repositories())),
				2024);

		StatisticsDocument statistics = await collector.CollectAsync("token", maxRetries: 1, CancellationToken.None);

		// 12 months, each accepted at its partial value of 1 after retrying.
		Assert.AreEqual(12u, statistics.CommitContributions);

		// 1 year + 2 halves + 4 quarters + 12 months, each month retried 3 times.
		Assert.HasCount(1 + 2 + 4 + (12 * 4), handler.TotalsRanges);
	}

	/// <summary>A repository seen in two ranges is recorded once.</summary>
	[TestMethod]
	public async Task CollectDeduplicatesRepositoriesAcrossRanges()
	{
		(StatisticsCollector collector, _) = Build(
				totals: _ => Ok(Data(""" "totalCommitContributions": 1 """)),
				commits: call => call == 0
						? Ok(DataWithErrors(Repositories(), ResourceLimitError))
						: Ok(Data(Repositories("ada/one"))),
				2024);

		StatisticsDocument statistics = await collector.CollectAsync("token", maxRetries: 1, CancellationToken.None);

		Assert.HasCount(1, statistics.Repositories);
		Assert.AreEqual("ada/one", statistics.Repositories[0].Name);
		Assert.AreEqual(7u, statistics.Repositories[0].Views);
		Assert.AreEqual(15u, statistics.Repositories[0].LinesChanged);
	}
}
