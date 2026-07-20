using BB84.GitHub.Statistics.GitHub;
using BB84.GitHub.Statistics.Statistics;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace BB84.GitHub.Statistics.Tests;

/// <summary>
/// Covers the behaviour that motivated writing a raw HTTP client instead of using
/// Octokit: the contributor-statistics status code has to drive control flow.
/// </summary>
[TestClass]
public sealed class LinesChangedResolverTests
{
	private sealed class StubHandler(Func<int, (HttpStatusCode Status, string Body)> respond) : HttpMessageHandler
	{
		public int CallCount { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
		{
			(HttpStatusCode status, string body) = respond(CallCount++);
			return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
		}
	}

	private sealed class StubGit : GitCli
	{
		public StubGit()
				: base(NullLogger<GitCli>.Instance)
		{
		}

		public int CallCount { get; private set; }

		public uint Result { get; init; } = 4242;

		public override Task<uint> GetLinesChangedAsync(
				string login,
				string token,
				string repo,
				IReadOnlyList<string> emails,
				CancellationToken ct)
		{
			CallCount++;
			return Task.FromResult(Result);
		}
	}

	private static (LinesChangedResolver Resolver, StubHandler Handler, StubGit Git) Build(
			Func<int, (HttpStatusCode, string)> respond)
	{
		StubHandler handler = new(respond);
		GitHubApiClient client = new(new HttpClient(handler), NullLogger<GitHubApiClient>.Instance);
		StubGit git = new();

		LinesChangedResolver resolver = new(client, git, NullLogger<LinesChangedResolver>.Instance)
		{
			Delay = static (_, _) => Task.CompletedTask,
			UtcNowSeconds = static () => 0,
		};

		return (resolver, handler, git);
	}

	private static StatisticsDocument Document(params string[] repos) => new()
	{
		User = "ada",
		Emails = ["ada@example.com"],
		Repositories = [.. repos.Select(static r => new RepositoryStats { Name = r })],
	};

	[TestMethod]
	public async Task ResolveAllParsesLinesForMatchingAuthor()
	{
		const string body = """
            [
              { "author": { "login": "ada" },   "weeks": [ { "a": 10, "d": 5 }, { "a": 1, "d": 0 } ] },
              { "author": { "login": "other" }, "weeks": [ { "a": 99, "d": 99 } ] }
            ]
            """;

		(LinesChangedResolver resolver, _, StubGit git) = Build(_ => (HttpStatusCode.OK, body));
		StatisticsDocument document = Document("a/one");

		await resolver.ResolveAllAsync(document, "token", maxRetries: 5, CancellationToken.None);

		Assert.AreEqual(16u, document.Repositories[0].LinesChanged);
		Assert.AreEqual(0, git.CallCount, "the clone fallback must not run when the API answers");
	}

	/// <summary>
	/// GitHub returns 202 indefinitely for many repositories. Octokit would hide
	/// this and yield an empty list; here it must retry and then clone.
	/// </summary>
	[TestMethod]
	public async Task ResolveAllFallsBackToCloningAfterMaxRetries()
	{
		(LinesChangedResolver resolver, StubHandler handler, StubGit git) =
				Build(_ => (HttpStatusCode.Accepted, string.Empty));

		StatisticsDocument document = Document("a/one");

		await resolver.ResolveAllAsync(document, "token", maxRetries: 2, CancellationToken.None);

		Assert.AreEqual(3, handler.CallCount, "should attempt once plus two retries before cloning");
		Assert.AreEqual(1, git.CallCount);
		Assert.AreEqual(4242u, document.Repositories[0].LinesChanged);
	}

	[TestMethod]
	[DataRow(HttpStatusCode.Accepted)]
	[DataRow(HttpStatusCode.Forbidden)]
	[DataRow(HttpStatusCode.TooManyRequests)]
	public async Task ResolveAllTreatsThrottlingStatusesAsRetryable(HttpStatusCode status)
	{
		(LinesChangedResolver resolver, StubHandler handler, StubGit git) = Build(_ => (status, string.Empty));

		await resolver.ResolveAllAsync(Document("a/one"), "token", maxRetries: 1, CancellationToken.None);

		Assert.AreEqual(2, handler.CallCount);
		Assert.AreEqual(1, git.CallCount);
	}

	[TestMethod]
	public async Task ResolveAllSucceedsOnARetryWithoutCloning()
	{
		(LinesChangedResolver resolver, StubHandler handler, StubGit git) = Build(call => call == 0
				? (HttpStatusCode.Accepted, string.Empty)
				: (HttpStatusCode.OK, """[ { "author": { "login": "ada" }, "weeks": [ { "a": 7, "d": 3 } ] } ]"""));

		StatisticsDocument document = Document("a/one");

		await resolver.ResolveAllAsync(document, "token", maxRetries: 5, CancellationToken.None);

		Assert.AreEqual(2, handler.CallCount);
		Assert.AreEqual(0, git.CallCount);
		Assert.AreEqual(10u, document.Repositories[0].LinesChanged);
	}

	/// <summary>A 200 with an unreadable body resolves the repository at zero rather than failing the run.</summary>
	[TestMethod]
	public async Task ResolveAllTolerantOfUnparseableSuccessBody()
	{
		(LinesChangedResolver resolver, StubHandler handler, StubGit git) =
				Build(_ => (HttpStatusCode.OK, "{ this is not the array we expect }"));

		StatisticsDocument document = Document("a/one");

		await resolver.ResolveAllAsync(document, "token", maxRetries: 5, CancellationToken.None);

		Assert.AreEqual(1, handler.CallCount);
		Assert.AreEqual(0, git.CallCount);
		Assert.AreEqual(0u, document.Repositories[0].LinesChanged);
	}

	[TestMethod]
	public async Task ResolveAllThrowsOnUnexpectedStatus()
	{
		(LinesChangedResolver resolver, _, _) = Build(_ => (HttpStatusCode.InternalServerError, string.Empty));

		_ = await Assert.ThrowsExactlyAsync<HttpRequestException>(
				() => resolver.ResolveAllAsync(Document("a/one"), "token", maxRetries: 5, CancellationToken.None));
	}

	[TestMethod]
	public async Task ResolveAllSkipsRepositoriesThatAlreadyHaveCounts()
	{
		(LinesChangedResolver resolver, StubHandler handler, _) = Build(_ => (HttpStatusCode.OK, "[]"));

		StatisticsDocument document = Document("a/one");
		document.Repositories[0].LinesChanged = 99;

		await resolver.ResolveAllAsync(document, "token", maxRetries: 5, CancellationToken.None);

		Assert.AreEqual(0, handler.CallCount);
		Assert.AreEqual(99u, document.Repositories[0].LinesChanged);
	}
}
