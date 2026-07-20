using BB84.GitHub.Statistics.GitHub.Models;
using BB84.GitHub.Statistics.Statistics;
using System.Text.Json.Serialization;

namespace BB84.GitHub.Statistics.GitHub;

/// <summary>
/// Serializer context for the statistics document. snake_case and two-space
/// indentation match the Zig output so existing <c>stats.json</c> files and any
/// <c>jq</c> pipelines built against them keep working.
/// </summary>
[JsonSourceGenerationOptions(
		PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
		WriteIndented = true)]
[JsonSerializable(typeof(StatisticsDocument))]
internal sealed partial class StatisticsJsonContext : JsonSerializerContext;

/// <summary>Serializer context for GitHub API request and response payloads.</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GraphQlRequest))]
[JsonSerializable(typeof(GraphQlResponse<ViewerWrapper<BasicInfoViewer>>))]
[JsonSerializable(typeof(GraphQlResponse<ViewerWrapper<ContributionsViewer>>))]
[JsonSerializable(typeof(List<UserEmail>))]
[JsonSerializable(typeof(TrafficViews))]
[JsonSerializable(typeof(List<ContributorStats>))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext;
