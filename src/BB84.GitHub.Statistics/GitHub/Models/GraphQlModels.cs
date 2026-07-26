using System.Text.Json.Serialization;

namespace BB84.GitHub.Statistics.GitHub.Models;

/// <summary>
/// GraphQL response shapes. These carry explicit <see cref="JsonPropertyNameAttribute"/>
/// values because GitHub returns camelCase while the serializer is configured for
/// the snake_case statistics document.
/// </summary>
internal sealed class GraphQlResponse<T>
{
	[JsonPropertyName("data")]
	public T? Data { get; set; }

	/// <summary>
	/// GraphQL reports failures in the body with an HTTP 200, so this has to be
	/// inspected on every response. A <c>RESOURCE_LIMITS_EXCEEDED</c> error in
	/// particular arrives alongside partial data, where the fields GitHub gave up
	/// on are simply absent and would otherwise deserialize to zero.
	/// </summary>
	[JsonPropertyName("errors")]
	public List<GraphQlError>? Errors { get; set; }
}

internal sealed class GraphQlError
{
	[JsonPropertyName("type")]
	public string? Type { get; set; }

	[JsonPropertyName("message")]
	public string? Message { get; set; }
}

internal sealed class ViewerWrapper<T>
{
	[JsonPropertyName("viewer")]
	public T? Viewer { get; set; }
}

internal sealed class BasicInfoViewer
{
	[JsonPropertyName("login")]
	public string Login { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("contributionsCollection")]
	public ContributionYears? ContributionsCollection { get; set; }
}

internal sealed class ContributionYears
{
	[JsonPropertyName("contributionYears")]
	public List<int> Years { get; set; } = [];
}

/// <summary>A GraphQL connection reduced to its <c>totalCount</c>.</summary>
internal sealed class TotalCount
{
	[JsonPropertyName("totalCount")]
	public long Count { get; set; }
}

/// <summary>The owned-repository connection, which also reports disk usage.</summary>
internal sealed class RepositoryConnectionSummary
{
	[JsonPropertyName("totalCount")]
	public long Count { get; set; }

	/// <summary>Kilobytes, per the GitHub schema. Null when the token cannot see it.</summary>
	[JsonPropertyName("totalDiskUsage")]
	public long? TotalDiskUsage { get; set; }
}

internal sealed class ProfileViewer
{
	[JsonPropertyName("createdAt")]
	public string? CreatedAt { get; set; }

	[JsonPropertyName("followers")]
	public TotalCount? Followers { get; set; }

	[JsonPropertyName("following")]
	public TotalCount? Following { get; set; }

	[JsonPropertyName("starredRepositories")]
	public TotalCount? StarredRepositories { get; set; }

	[JsonPropertyName("gists")]
	public TotalCount? Gists { get; set; }

	[JsonPropertyName("organizations")]
	public TotalCount? Organizations { get; set; }

	[JsonPropertyName("watching")]
	public TotalCount? Watching { get; set; }

	[JsonPropertyName("sponsors")]
	public TotalCount? Sponsors { get; set; }

	[JsonPropertyName("mergedPullRequests")]
	public TotalCount? MergedPullRequests { get; set; }

	[JsonPropertyName("repositories")]
	public RepositoryConnectionSummary? Repositories { get; set; }
}

internal sealed class CalendarViewer
{
	[JsonPropertyName("contributionsCollection")]
	public CalendarCollection? ContributionsCollection { get; set; }
}

internal sealed class CalendarCollection
{
	[JsonPropertyName("restrictedContributionsCount")]
	public long RestrictedContributionsCount { get; set; }

	[JsonPropertyName("contributionCalendar")]
	public ContributionCalendarData? ContributionCalendar { get; set; }
}

internal sealed class ContributionCalendarData
{
	[JsonPropertyName("totalContributions")]
	public long TotalContributions { get; set; }

	[JsonPropertyName("weeks")]
	public List<CalendarWeek>? Weeks { get; set; }
}

internal sealed class CalendarWeek
{
	[JsonPropertyName("contributionDays")]
	public List<CalendarDay>? ContributionDays { get; set; }
}

internal sealed class CalendarDay
{
	/// <summary>ISO <c>yyyy-MM-dd</c>.</summary>
	[JsonPropertyName("date")]
	public string? Date { get; set; }

	[JsonPropertyName("contributionCount")]
	public int ContributionCount { get; set; }
}

internal sealed class ContributionsViewer
{
	[JsonPropertyName("contributionsCollection")]
	public ContributionsCollection? ContributionsCollection { get; set; }
}

internal sealed class ContributionsCollection
{
	[JsonPropertyName("totalRepositoryContributions")]
	public uint TotalRepositoryContributions { get; set; }

	[JsonPropertyName("totalIssueContributions")]
	public uint TotalIssueContributions { get; set; }

	[JsonPropertyName("totalCommitContributions")]
	public uint TotalCommitContributions { get; set; }

	[JsonPropertyName("totalPullRequestContributions")]
	public uint TotalPullRequestContributions { get; set; }

	[JsonPropertyName("totalPullRequestReviewContributions")]
	public uint TotalPullRequestReviewContributions { get; set; }

	[JsonPropertyName("commitContributionsByRepository")]
	public List<CommitContributionByRepository> CommitContributionsByRepository { get; set; } = [];
}

internal sealed class CommitContributionByRepository
{
	[JsonPropertyName("repository")]
	public GraphQlRepository? Repository { get; set; }
}

internal sealed class GraphQlRepository
{
	[JsonPropertyName("nameWithOwner")]
	public string NameWithOwner { get; set; } = string.Empty;

	[JsonPropertyName("stargazerCount")]
	public uint StargazerCount { get; set; }

	[JsonPropertyName("forkCount")]
	public uint ForkCount { get; set; }

	[JsonPropertyName("isPrivate")]
	public bool IsPrivate { get; set; }

	[JsonPropertyName("languages")]
	public GraphQlLanguageConnection? Languages { get; set; }
}

internal sealed class GraphQlLanguageConnection
{
	[JsonPropertyName("edges")]
	public List<GraphQlLanguageEdge>? Edges { get; set; }
}

internal sealed class GraphQlLanguageEdge
{
	[JsonPropertyName("size")]
	public uint Size { get; set; }

	[JsonPropertyName("node")]
	public GraphQlLanguageNode? Node { get; set; }
}

internal sealed class GraphQlLanguageNode
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;

	[JsonPropertyName("color")]
	public string? Color { get; set; }
}

/// <summary>Request envelope for a GraphQL call.</summary>
internal sealed class GraphQlRequest
{
	[JsonPropertyName("query")]
	public string Query { get; set; } = string.Empty;

	[JsonPropertyName("variables")]
	public DateRangeVariables? Variables { get; set; }
}

/// <summary>
/// The only GraphQL variables this application sends. Kept concrete rather than
/// a dictionary so the source-generated serializer can handle it under AOT.
/// </summary>
internal sealed class DateRangeVariables
{
	[JsonPropertyName("from")]
	public string From { get; set; } = string.Empty;

	[JsonPropertyName("to")]
	public string To { get; set; } = string.Empty;
}
