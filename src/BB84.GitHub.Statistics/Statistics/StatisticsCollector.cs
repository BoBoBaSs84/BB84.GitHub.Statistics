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

	/// <summary>
	/// GraphQL error type returned when a query exceeds GitHub's per-query compute
	/// budget. It arrives with an HTTP 200 and partial data, so it has to be read
	/// out of the response body.
	/// </summary>
	private const string ResourceLimitsExceeded = "RESOURCE_LIMITS_EXCEEDED";

	/// <summary>
	/// Attempts at the one-month floor, where the range can no longer be
	/// subdivided. The limit is load-dependent, so simply asking again often works.
	/// </summary>
	private const int ResourceLimitRetries = 3;

	/// <summary>Injected so tests do not actually sleep.</summary>
	public Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = Task.Delay;

	/// <summary>
	/// Injected so the current-streak calculation is deterministic in tests, which
	/// would otherwise depend on the day they run.
	/// </summary>
	public Func<DateOnly> UtcToday { get; init; } = static () => DateOnly.FromDateTime(DateTime.UtcNow);

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

		await CollectProfileAsync(statistics, ct).ConfigureAwait(false);

		// Keyed by date so the overlapping edges GitHub includes at the start and
		// end of each calendar year collapse instead of double-counting.
		Dictionary<DateOnly, int> days = [];

		int latestYear = info.Years.Count > 0 ? info.Years.Max() : 0;

		HashSet<string> seen = new(StringComparer.Ordinal);
		foreach (int year in info.Years)
		{
			await CollectTotalsAsync(statistics, year, startMonth: 0, months: 12, ct).ConfigureAwait(false);
			await CollectCalendarAsync(statistics, days, year, year == latestYear, ct).ConfigureAwait(false);
			await CollectRangeAsync(statistics, seen, year, startMonth: 0, months: 12, ct).ConfigureAwait(false);
		}

		ApplyStreaks(statistics, days);

		statistics.Repositories = [.. statistics.Repositories
						.OrderByDescending(static r => r.Views)
						.ThenByDescending(static r => (long)r.Stars + r.Forks)];

		return statistics;
	}

	/// <summary>
	/// Fills in the profile counters. A failure here is logged and left as zero
	/// rather than thrown: these are decorative, and <c>sponsors</c> or
	/// <c>totalDiskUsage</c> failing on a narrowly scoped token must not cost the
	/// caller the entire run.
	/// </summary>
	private async Task CollectProfileAsync(StatisticsDocument statistics, CancellationToken ct)
	{
		Log.GettingProfile(logger);

		ApiResponse response = await client.GraphQlAsync(GraphQlQueries.ProfileInfo, null, ct).ConfigureAwait(false);
		if (!response.IsOk)
		{
			Log.ProfileFailed(logger, response.Status);
			return;
		}

		ProfileViewer? viewer = client.TryDeserialize(
				response,
				GitHubJsonContext.Default.GraphQlResponseViewerWrapperProfileViewer,
				"profile counters")
				?.Data?.Viewer;

		if (viewer is null)
		{
			Log.ProfileFailed(logger, response.Status);
			return;
		}

		statistics.CreatedAt = viewer.CreatedAt;
		statistics.Followers = viewer.Followers?.Count ?? 0;
		statistics.Following = viewer.Following?.Count ?? 0;
		statistics.StarsGiven = viewer.StarredRepositories?.Count ?? 0;
		statistics.Gists = viewer.Gists?.Count ?? 0;
		statistics.Organizations = viewer.Organizations?.Count ?? 0;
		statistics.Watching = viewer.Watching?.Count ?? 0;
		statistics.Sponsors = viewer.Sponsors?.Count ?? 0;
		statistics.MergedPullRequests = viewer.MergedPullRequests?.Count ?? 0;
		statistics.OwnRepositories = viewer.Repositories?.Count ?? 0;
		statistics.DiskUsageKb = viewer.Repositories?.TotalDiskUsage ?? 0;
	}

	/// <summary>
	/// Accumulates one year of the daily contribution calendar into
	/// <paramref name="days"/>. Degrades to no data on failure, for the same reason
	/// as <see cref="CollectProfileAsync"/>.
	/// </summary>
	private async Task CollectCalendarAsync(
			StatisticsDocument statistics,
			Dictionary<DateOnly, int> days,
			int year,
			bool isLatestYear,
			CancellationToken ct)
	{
		Log.GettingCalendar(logger, year);

		ApiResponse response = await client
				.GraphQlAsync(GraphQlQueries.ContributionCalendar, BuildRange(year, startMonth: 0, months: 12), ct)
				.ConfigureAwait(false);

		if (!response.IsOk)
		{
			Log.CalendarFailed(logger, year, response.Status);
			return;
		}

		CalendarCollection? collection = client.TryDeserialize(
				response,
				GitHubJsonContext.Default.GraphQlResponseViewerWrapperCalendarViewer,
				$"contribution calendar for {year}")
				?.Data?.Viewer?.ContributionsCollection;

		if (collection is null)
		{
			Log.CalendarFailed(logger, year, response.Status);
			return;
		}

		statistics.PrivateContributions += collection.RestrictedContributionsCount;

		if (isLatestYear)
		{
			statistics.ContributionsThisYear = collection.ContributionCalendar?.TotalContributions ?? 0;
		}

		foreach (CalendarWeek week in collection.ContributionCalendar?.Weeks ?? [])
		{
			foreach (CalendarDay day in week.ContributionDays ?? [])
			{
				if (DateOnly.TryParse(day.Date, CultureInfo.InvariantCulture, out DateOnly date))
				{
					days[date] = Math.Max(days.GetValueOrDefault(date), day.ContributionCount);
				}
			}
		}
	}

	/// <summary>
	/// Derives the streak figures from the accumulated calendar and stores them on
	/// the document, so that <c>stats.json</c> carries the answers rather than the
	/// several thousand raw days they came from.
	/// </summary>
	private void ApplyStreaks(StatisticsDocument statistics, Dictionary<DateOnly, int> days)
	{
		if (days.Count == 0)
		{
			return;
		}

		List<DateOnly> active = [.. days.Where(static d => d.Value > 0).Select(static d => d.Key).Order()];

		KeyValuePair<DateOnly, int> busiest = days
				.OrderByDescending(static d => d.Value)
				.ThenBy(static d => d.Key)
				.First();

		if (busiest.Value > 0)
		{
			statistics.BusiestDay = busiest.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
			statistics.BusiestDayCount = busiest.Value;
		}

		long longest = 0, run = 0;
		DateOnly previous = default;
		foreach (DateOnly date in active)
		{
			run = run > 0 && date == previous.AddDays(1) ? run + 1 : 1;
			longest = Math.Max(longest, run);
			previous = date;
		}

		statistics.LongestStreak = longest;

		// The run only counts as current if it reaches yesterday; a day that is
		// still in progress has not broken anything, so today being empty is fine.
		DateOnly today = UtcToday();
		statistics.CurrentStreak = active.Count > 0 && (previous == today || previous == today.AddDays(-1))
				? run
				: 0;
	}

	/// <summary>The outcome of one contributions query.</summary>
	/// <remarks>
	/// <see cref="ResourceLimited"/> means GitHub abandoned the query part way
	/// through. <see cref="Collection"/> may still be populated in that case, but
	/// the fields GitHub gave up on are missing and read back as zero, so it must
	/// never be treated as a complete answer.
	/// </remarks>
	private readonly record struct QueryResult(ContributionsCollection? Collection, bool ResourceLimited);

	/// <summary>Prime factors of 12, applied in order to narrow 12 -> 6 -> 3 -> 1 months.</summary>
	private static readonly int[] SubdivisionFactors = [2, 3];

	/// <summary>
	/// Sends one contributions query and reports whether GitHub gave up on it.
	/// </summary>
	/// <remarks>
	/// GraphQL reports failures in the body with an HTTP 200. Only
	/// <c>RESOURCE_LIMITS_EXCEEDED</c> is recoverable, by asking for less at a
	/// time; anything else is surfaced rather than silently counted as zero.
	/// </remarks>
	private async Task<QueryResult> QueryAsync(
			string query,
			DateRangeVariables variables,
			int year,
			CancellationToken ct)
	{
		ApiResponse response = await client.GraphQlAsync(query, variables, ct).ConfigureAwait(false);

		if (!response.IsOk)
		{
			Log.RangeFailed(logger, year, response.Status);
			throw new HttpRequestException($"Failed to get data from {year} ({response.Status}).");
		}

		GraphQlResponse<ViewerWrapper<ContributionsViewer>>? parsed = System.Text.Json.JsonSerializer
				.Deserialize(response.Body, GitHubJsonContext.Default.GraphQlResponseViewerWrapperContributionsViewer);

		bool resourceLimited = false;
		if (parsed?.Errors is { Count: > 0 } errors)
		{
			resourceLimited = errors.Any(
					static e => string.Equals(e.Type, ResourceLimitsExceeded, StringComparison.Ordinal));

			if (!resourceLimited)
			{
				string messages = string.Join(
						"; ",
						errors.Select(static e => e.Message ?? e.Type ?? "unknown error"));

				throw new HttpRequestException($"The contributions query for {year} failed: {messages}");
			}
		}

		ContributionsCollection? collection = parsed?.Data?.Viewer?.ContributionsCollection;

		return collection is null && !resourceLimited
				? throw new HttpRequestException($"The contributions query for {year} returned no data.")
				: new QueryResult(collection, resourceLimited);
	}

	/// <summary>
	/// Divides a range by increasingly large prime factors of 12 and runs
	/// <paramref name="recurse"/> over each piece, narrowing 12 -> 6 -> 3 -> 1
	/// months. Returns <c>false</c> at one month, where no factor divides.
	/// </summary>
	private static async Task<bool> TrySubdivideAsync(int startMonth, int months, Func<int, int, Task> recurse)
	{
		foreach (int factor in SubdivisionFactors)
		{
			if (months % factor != 0)
			{
				continue;
			}

			int slice = months / factor;
			for (int i = 0; i < factor; i++)
			{
				await recurse(startMonth + (slice * i), slice).ConfigureAwait(false);
			}

			return true;
		}

		return false;
	}

	/// <summary>
	/// Re-issues a query for a range that can no longer be subdivided. Past
	/// <see cref="ResourceLimitRetries"/> the partial data is accepted with a
	/// warning rather than failing the run, matching how the repository cap
	/// already degrades.
	/// </summary>
	private async Task<QueryResult> RetryAtFloorAsync(
			string query,
			int year,
			int startMonth,
			int months,
			QueryResult limited,
			CancellationToken ct)
	{
		DateRangeVariables variables = BuildRange(year, startMonth, months);

		for (int attempt = 1; attempt <= ResourceLimitRetries; attempt++)
		{
			Log.ResourceLimitRetrying(logger, attempt, ResourceLimitRetries, startMonth + 1, year);
			await Delay(TimeSpan.FromSeconds(attempt), ct).ConfigureAwait(false);

			limited = await QueryAsync(query, variables, year, ct).ConfigureAwait(false);
			if (!limited.ResourceLimited)
			{
				return limited;
			}
		}

		Log.ResourceLimitReached(logger, startMonth + 1, year);
		return limited;
	}

	/// <summary>
	/// Adds the five contribution totals for a range.
	/// </summary>
	/// <remarks>
	/// Unlike <see cref="CollectRangeAsync"/> this subdivides only when GitHub
	/// refuses the query, so the normal case is one request per year rather than
	/// one per subdivided range. The totals are all "created within the range"
	/// counts, so summing the pieces gives the same answer as asking for the whole.
	/// </remarks>
	private async Task CollectTotalsAsync(
			StatisticsDocument statistics,
			int year,
			int startMonth,
			int months,
			CancellationToken ct)
	{
		QueryResult result = await QueryAsync(
				GraphQlQueries.ContributionTotals,
				BuildRange(year, startMonth, months),
				year,
				ct).ConfigureAwait(false);

		if (result.ResourceLimited)
		{
			Log.ResourceLimitSubdividing(logger, months, startMonth + 1, year);

			Task Recurse(int start, int slice) => CollectTotalsAsync(statistics, year, start, slice, ct);

			if (await TrySubdivideAsync(startMonth, months, Recurse).ConfigureAwait(false))
			{
				return;
			}

			result = await RetryAtFloorAsync(
					GraphQlQueries.ContributionTotals,
					year,
					startMonth,
					months,
					result,
					ct).ConfigureAwait(false);
		}

		if (result.Collection is not { } totals)
		{
			return;
		}

		statistics.RepoContributions += totals.TotalRepositoryContributions;
		statistics.IssueContributions += totals.TotalIssueContributions;
		statistics.CommitContributions += totals.TotalCommitContributions;
		statistics.PrContributions += totals.TotalPullRequestContributions;
		statistics.ReviewContributions += totals.TotalPullRequestReviewContributions;
	}

	/// <summary>
	/// Collects the repositories contributed to in one date range, subdividing
	/// when the result saturates GitHub's 100-repository cap or when GitHub
	/// refuses the query outright.
	/// </summary>
	/// <remarks>
	/// The range starts at a full year and narrows 12 -> 6 -> 3 -> 1 months. At
	/// one month no factor divides, and the data is accepted with a warning rather
	/// than lost silently.
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

		QueryResult result = await QueryAsync(
				GraphQlQueries.CommitContributionsByRange,
				BuildRange(year, startMonth, months),
				year,
				ct).ConfigureAwait(false);

		int count = result.Collection?.CommitContributionsByRepository.Count ?? 0;
		Log.ParsedRepositories(logger, count, year);

		Task Recurse(int start, int slice) => CollectRangeAsync(statistics, seen, year, start, slice, ct);

		if (result.ResourceLimited)
		{
			Log.ResourceLimitSubdividing(logger, months, startMonth + 1, year);

			if (await TrySubdivideAsync(startMonth, months, Recurse).ConfigureAwait(false))
			{
				return;
			}

			result = await RetryAtFloorAsync(
					GraphQlQueries.CommitContributionsByRange,
					year,
					startMonth,
					months,
					result,
					ct).ConfigureAwait(false);

			count = result.Collection?.CommitContributionsByRepository.Count ?? 0;
		}

		if (count >= RepositoryLimit)
		{
			if (await TrySubdivideAsync(startMonth, months, Recurse).ConfigureAwait(false))
			{
				return;
			}

			Log.RepositoryLimitReached(logger, RepositoryLimit, startMonth + 1, year);
		}

		if (result.Collection is not { } collection)
		{
			return;
		}

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

	/// <summary>
	/// The half-open range covering <paramref name="months"/> starting at
	/// <paramref name="startMonth"/>, which rolls into the following year when the
	/// range reaches December.
	/// </summary>
	private static DateRangeVariables BuildRange(int year, int startMonth, int months) => new()
	{
		From = FormatTimestamp(year, startMonth + 1),
		To = FormatTimestamp(year + ((startMonth + months) / 12), ((startMonth + months) % 12) + 1),
	};

	private static string FormatTimestamp(int year, int month) =>
			string.Create(CultureInfo.InvariantCulture, $"{year}-{month:00}-01T00:00:00Z");
}
