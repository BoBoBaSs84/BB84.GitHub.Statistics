using BB84.GitHub.Statistics.GitHub;
using BB84.GitHub.Statistics.GitHub.Models;
using BB84.GitHub.Statistics.Logging;
using Microsoft.Extensions.Logging;
using System.Net;

namespace BB84.GitHub.Statistics.Statistics;

/// <summary>
/// Resolves lines changed per repository from GitHub's contributor-statistics
/// endpoint, falling back to a local clone when the endpoint will not answer.
/// </summary>
/// <remarks>
/// <c>/stats/contributors</c> replies 202 while it computes, and 403/429 when
/// rate limited. For many repositories it now returns 202 indefinitely
/// (community discussion 192970), so the clone fallback carries most real
/// workloads and is the part most worth testing.
/// </remarks>
internal sealed class LinesChangedResolver(
		GitHubApiClient client,
		GitCli git,
		ILogger<LinesChangedResolver> logger)
{
	private sealed class PendingRepo(RepositoryStats repository)
	{
		public RepositoryStats Repository { get; } = repository;

		public long Delay { get; set; }

		public long Timestamp { get; set; }

		public int Retries { get; set; }
	}

	/// <summary>Injected so tests do not actually sleep.</summary>
	public Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = Task.Delay;

	public Func<long> UtcNowSeconds { get; init; } = static () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

	/// <summary>
	/// Fetches lines changed for a single repository. Returns the status so the
	/// caller can decide whether to retry, which is the whole reason this does
	/// not go through Octokit.
	/// </summary>
	public async Task<HttpStatusCode> FetchAsync(
			RepositoryStats repository,
			string user,
			CancellationToken ct)
	{
		Log.TryingLinesChanged(logger, repository.Name);

		ApiResponse response = await client
				.GetAsync($"https://api.github.com/repos/{repository.Name}/stats/contributors", ct)
				.ConfigureAwait(false);

		if (!response.IsOk)
		{
			return response.Status;
		}

		List<ContributorStats>? authors = client.TryDeserialize(
				response,
				GitHubJsonContext.Default.ListContributorStats,
				$"lines changed by {user} in {repository.Name}");

		if (authors is null)
		{
			// GitHub answered 200 with a body we cannot read. Treat the repository
			// as resolved at zero rather than failing the whole run.
			return response.Status;
		}

		uint linesChanged = 0;
		foreach (ContributorStats author in authors)
		{
			if (author.Author is null || !string.Equals(author.Author.Login, user, StringComparison.Ordinal))
			{
				continue;
			}

			foreach (ContributorWeek week in author.Weeks)
			{
				linesChanged += week.Additions;
				linesChanged += week.Deletions;
			}
		}

		repository.LinesChanged = linesChanged;
		string plural = Log.Plural(linesChanged);
		Log.GotLinesChanged(logger, linesChanged, plural, user, repository.Name);

		return response.Status;
	}

	/// <summary>
	/// Drains every repository still reporting zero lines changed, retrying
	/// throttled responses and cloning once <paramref name="maxRetries"/> is exceeded.
	/// </summary>
	public async Task ResolveAllAsync(
			StatisticsDocument statistics,
			string token,
			int? maxRetries,
			CancellationToken ct)
	{
		PriorityQueue<PendingRepo, long> queue = new();
		foreach (RepositoryStats repository in statistics.Repositories)
		{
			if (repository.LinesChanged > 0)
			{
				continue;
			}

			PendingRepo pending = new(repository) { Timestamp = UtcNowSeconds() };
			queue.Enqueue(pending, pending.Timestamp);
		}

		while (queue.TryDequeue(out PendingRepo? item, out _))
		{
			long now = UtcNowSeconds();
			if (item.Timestamp > now)
			{
				long delay = item.Timestamp - now;
				int remaining = queue.Count + 1;
				string plural = Log.Plural(remaining);
				Log.SleepingBeforeRetry(logger, delay, remaining, plural);
				await Delay(TimeSpan.FromSeconds(delay), ct).ConfigureAwait(false);
			}

			HttpStatusCode status = await FetchAsync(item.Repository, statistics.User, ct).ConfigureAwait(false);

			switch (status)
			{
				case HttpStatusCode.OK:
					break;

				case HttpStatusCode.Accepted:
				case HttpStatusCode.Forbidden:
				case HttpStatusCode.TooManyRequests:
					// The previous delay is applied now, then a fresh one is drawn
					// for the next attempt, so the first retry is immediate.
					item.Timestamp = UtcNowSeconds() + item.Delay;

					// This works far better with a very short delay than with
					// exponential backoff, hence the flat 0-4s range.
					item.Delay = Random.Shared.Next(0, 5);
					item.Retries++;

					if (maxRetries is not { } max || item.Retries <= max)
					{
						queue.Enqueue(item, item.Timestamp);
					}
					else
					{
						await CloneAndCountAsync(item.Repository, statistics, token, ct).ConfigureAwait(false);
					}

					break;

				default:
					Log.ContributionDataFailed(logger, item.Repository.Name, status);
					throw new HttpRequestException($"Request failed with response {status}.");
			}
		}
	}

	private async Task CloneAndCountAsync(
			RepositoryStats repository,
			StatisticsDocument statistics,
			string token,
			CancellationToken ct)
	{
		Log.CloningRepository(logger, repository.Name);

		repository.LinesChanged = await git
				.GetLinesChangedAsync(statistics.User, token, repository.Name, statistics.Emails, ct)
				.ConfigureAwait(false);

		string plural = Log.Plural(repository.LinesChanged);
		Log.GotLinesChanged(logger, repository.LinesChanged, plural, statistics.User, repository.Name);
	}
}
