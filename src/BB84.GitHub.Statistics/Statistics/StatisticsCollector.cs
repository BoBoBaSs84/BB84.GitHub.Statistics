using BB84.GitHub.Statistics.GitHub;
using BB84.GitHub.Statistics.GitHub.Models;
using BB84.GitHub.Statistics.Logging;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace BB84.GitHub.Statistics.Statistics;

/// <summary>Collects the full statistics document from the GitHub API.</summary>
internal sealed class StatisticsCollector(
		GitHubApiClient client,
		LinesChangedResolver linesChanged,
		ILogger<StatisticsCollector> logger)
{
	/// <summary>
	/// GitHub caps <c>commitContributionsByRepository</c> at 100 entries with no
	/// pagination, so a range returning this many is assumed to be truncated.
	/// </summary>
	private const int RepositoryLimit = 100;

	public async Task<StatisticsDocument> CollectAsync(string token, int? maxRetries, CancellationToken ct)
	{
		StatisticsDocument statistics = await CollectRepositoriesAsync(ct).ConfigureAwait(false);
		await linesChanged.ResolveAllAsync(statistics, token, maxRetries, ct).ConfigureAwait(false);
		return statistics;
	}

	private sealed record BasicInfo(IReadOnlyList<int> Years, string User, string? Name, List<string> Emails);

	private async Task<BasicInfo> GetBasicInfoAsync(CancellationToken ct)
	{
		Log.GettingContributionYears(logger);

		ApiResponse response = await client.GraphQlAsync(GraphQlQueries.BasicInfo, null, ct).ConfigureAwait(false);
		if (!response.IsOk)
		{
			Log.ContributionYearsFailed(logger, response.Status);
			throw new HttpRequestException($"Failed to get contribution years ({response.Status}).");
		}

		BasicInfoViewer? viewer = System.Text.Json.JsonSerializer
				.Deserialize(response.Body, GitHubJsonContext.Default.GraphQlResponseViewerWrapperBasicInfoViewer)
				?.Data?.Viewer
				?? throw new HttpRequestException("The viewer query returned no data.");

		Log.GettingContributorEmails(logger);

		List<string> emails = [];
		ApiResponse emailResponse = await client
				.GetAsync("https://api.github.com/user/emails", ct)
				.ConfigureAwait(false);

		if (emailResponse.IsOk)
		{
			List<UserEmail>? parsed = client.TryDeserialize(
					emailResponse,
					GitHubJsonContext.Default.ListUserEmail,
					"user emails");

			if (parsed is not null)
			{
				emails.AddRange(parsed.Select(static e => e.Email));
			}
		}
		else
		{
			Log.UserEmailsFailed(logger);
		}

		if (emails.Count == 0)
		{
			emails.Add($"{viewer.Login}@users.noreply.github.com");
		}

		return new BasicInfo(
				viewer.ContributionsCollection?.Years ?? [],
				viewer.Login,
				viewer.Name,
				emails);
	}

	private async Task<StatisticsDocument> CollectRepositoriesAsync(CancellationToken ct)
	{
		BasicInfo info = await GetBasicInfoAsync(ct).ConfigureAwait(false);

		if (info.Name is not null)
		{
			Log.GettingDataForNamedUser(logger, info.Name, info.User);
		}
		else
		{
			Log.GettingDataForUser(logger, info.User);
		}

		StatisticsDocument statistics = new()
		{
			User = info.User,
			Name = info.Name ?? info.User,
			Emails = info.Emails,
		};

		HashSet<string> seen = new(StringComparer.Ordinal);
		foreach (int year in info.Years)
		{
			await CollectRangeAsync(statistics, seen, year, startMonth: 0, months: 12, ct).ConfigureAwait(false);
		}

		statistics.Repositories = [.. statistics.Repositories
						.OrderByDescending(static r => r.Views)
						.ThenByDescending(static r => (long)r.Stars + r.Forks)];

		return statistics;
	}

	/// <summary>
	/// Collects one date range, subdividing when the result saturates GitHub's
	/// 100-repository cap.
	/// </summary>
	/// <remarks>
	/// The range starts at a full year and is divided by increasingly large prime
	/// factors of 12, so it narrows 12 -> 6 -> 3 -> 1 months. At one month no
	/// factor divides, and the data is accepted with a warning rather than lost
	/// silently.
	/// </remarks>
	private async Task CollectRangeAsync(
			StatisticsDocument statistics,
			HashSet<string> seen,
			int year,
			int startMonth,
			int months,
			CancellationToken ct)
	{
		string plural = Log.Plural(months);
		Log.GettingRange(logger, months, plural, startMonth + 1, year);

		DateRangeVariables variables = new()
		{
			From = FormatTimestamp(year, startMonth + 1),
			To = FormatTimestamp(year + ((startMonth + months) / 12), ((startMonth + months) % 12) + 1),
		};

		ApiResponse response = await client
				.GraphQlAsync(GraphQlQueries.ContributionsByRange, variables, ct)
				.ConfigureAwait(false);

		if (!response.IsOk)
		{
			Log.RangeFailed(logger, year, response.Status);
			throw new HttpRequestException($"Failed to get data from {year} ({response.Status}).");
		}

		ContributionsCollection? collection = System.Text.Json.JsonSerializer
				.Deserialize(response.Body, GitHubJsonContext.Default.GraphQlResponseViewerWrapperContributionsViewer)
				?.Data?.Viewer?.ContributionsCollection
				?? throw new HttpRequestException($"The contributions query for {year} returned no data.");

		Log.ParsedRepositories(logger, collection.CommitContributionsByRepository.Count, year);

		if (collection.CommitContributionsByRepository.Count >= RepositoryLimit)
		{
			foreach (int factor in (ReadOnlySpan<int>)[2, 3])
			{
				if (months % factor != 0)
				{
					continue;
				}

				for (int i = 0; i < factor; i++)
				{
					await CollectRangeAsync(
							statistics,
							seen,
							year,
							startMonth + ((months / factor) * i),
							months / factor,
							ct).ConfigureAwait(false);
				}

				return;
			}

			Log.RepositoryLimitReached(logger, RepositoryLimit, startMonth + 1, year);
		}

		statistics.RepoContributions += collection.TotalRepositoryContributions;
		statistics.IssueContributions += collection.TotalIssueContributions;
		statistics.CommitContributions += collection.TotalCommitContributions;
		statistics.PrContributions += collection.TotalPullRequestContributions;
		statistics.ReviewContributions += collection.TotalPullRequestReviewContributions;

		foreach (CommitContributionByRepository entry in collection.CommitContributionsByRepository)
		{
			if (entry.Repository is not { } raw)
			{
				continue;
			}

			if (!seen.Add(raw.NameWithOwner))
			{
				Log.SkippingSeenRepository(logger, raw.NameWithOwner);
				continue;
			}

			RepositoryStats repository = new()
			{
				Name = raw.NameWithOwner,
				Stars = raw.StargazerCount,
				Forks = raw.ForkCount,
				Private = raw.IsPrivate,
				Languages = MapLanguages(raw.Languages),
				Views = await GetViewsAsync(raw.NameWithOwner, ct).ConfigureAwait(false)
			};
			_ = await linesChanged.FetchAsync(repository, statistics.User, ct).ConfigureAwait(false);

			statistics.Repositories.Add(repository);
		}
	}

	private static List<LanguageStats>? MapLanguages(GraphQlLanguageConnection? languages)
	{
		if (languages?.Edges is not { } edges)
		{
			return null;
		}

		List<LanguageStats> result = new(edges.Count);
		foreach (GraphQlLanguageEdge edge in edges)
		{
			if (edge.Node is not { } node)
			{
				continue;
			}

			result.Add(new LanguageStats { Name = node.Name, Size = edge.Size, Color = node.Color });
		}

		return result;
	}

	private async Task<uint> GetViewsAsync(string repository, CancellationToken ct)
	{
		Log.GettingViews(logger, repository);

		ApiResponse response = await client
				.GetAsync($"https://api.github.com/repos/{repository}/traffic/views", ct)
				.ConfigureAwait(false);

		if (!response.IsOk)
		{
			Log.ViewsFailed(logger, repository, response.Status);
			return 0;
		}

		TrafficViews? views = client.TryDeserialize(
				response,
				GitHubJsonContext.Default.TrafficViews,
				$"views for {repository}");

		return views?.Count ?? 0;
	}

	private static string FormatTimestamp(int year, int month) =>
			string.Create(CultureInfo.InvariantCulture, $"{year}-{month:00}-01T00:00:00Z");
}
