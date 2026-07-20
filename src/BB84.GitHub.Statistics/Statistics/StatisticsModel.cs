using System.Text.Json.Serialization;

namespace BB84.GitHub.Statistics.Statistics;

/// <summary>
/// The full statistics document, and the on-disk schema for
/// <c>--json-output-file</c> / <c>--json-input-file</c>.
/// </summary>
/// <remarks>
/// Property declaration order and snake_case naming match the Zig
/// implementation so existing <c>stats.json</c> files round-trip unchanged.
/// </remarks>
internal sealed class StatisticsDocument
{
	public List<RepositoryStats> Repositories { get; set; } = [];

	public string User { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public List<string> Emails { get; set; } = [];

	public uint RepoContributions { get; set; }

	public uint IssueContributions { get; set; }

	public uint CommitContributions { get; set; }

	public uint PrContributions { get; set; }

	public uint ReviewContributions { get; set; }

	[JsonIgnore]
	public long TotalContributions =>
			(long)RepoContributions +
			IssueContributions +
			CommitContributions +
			PrContributions +
			ReviewContributions;
}

internal sealed class RepositoryStats
{
	public string Name { get; set; } = string.Empty;

	public uint Stars { get; set; }

	public uint Forks { get; set; }

	public List<LanguageStats>? Languages { get; set; }

	public uint LinesChanged { get; set; }

	public uint Views { get; set; }

	public bool Private { get; set; }
}

internal sealed class LanguageStats
{
	public string Name { get; set; } = string.Empty;

	public uint Size { get; set; }

	public string? Color { get; set; }
}
