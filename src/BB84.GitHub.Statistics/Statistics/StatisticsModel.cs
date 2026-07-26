using System.Text.Json.Serialization;

namespace BB84.GitHub.Statistics.Statistics;

/// <summary>
/// The full statistics document, and the on-disk schema for
/// <c>--json-output-file</c> / <c>--json-input-file</c>.
/// </summary>
/// <remarks>
/// <para>
/// Property declaration order and snake_case naming match the Zig
/// implementation, and every field it wrote is still read back, so existing
/// <c>stats.json</c> files load unchanged.
/// </para>
/// <para>
/// The schema is a superset of the Zig one: the profile and streak properties
/// below are appended after the original block, and absent keys deserialize to
/// zero. Documents written by this application therefore carry keys the Zig
/// implementation would not recognise.
/// </para>
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

	/// <summary>Account creation timestamp, ISO 8601. Null when the profile query failed.</summary>
	public string? CreatedAt { get; set; }

	public long Followers { get; set; }

	public long Following { get; set; }

	public long StarsGiven { get; set; }

	public long Gists { get; set; }

	public long Organizations { get; set; }

	public long Watching { get; set; }

	public long Sponsors { get; set; }

	public long MergedPullRequests { get; set; }

	/// <summary>Repositories the user owns, as opposed to has contributed to.</summary>
	public long OwnRepositories { get; set; }

	/// <summary>Total size of the owned repositories, in kilobytes.</summary>
	public long DiskUsageKb { get; set; }

	/// <summary>
	/// Consecutive days with at least one contribution, ending today or yesterday.
	/// </summary>
	/// <remarks>
	/// A snapshot as of collection time. Re-rendering an old <c>stats.json</c> with
	/// <c>--json-input-file</c> reports the streak as it stood when the document was
	/// written, not as it stands now.
	/// </remarks>
	public long CurrentStreak { get; set; }

	public long LongestStreak { get; set; }

	/// <summary>The single day with the most contributions, <c>yyyy-MM-dd</c>.</summary>
	public string? BusiestDay { get; set; }

	public long BusiestDayCount { get; set; }

	/// <summary>Calendar total for the most recent contribution year.</summary>
	public long ContributionsThisYear { get; set; }

	/// <summary>Contributions in repositories the token cannot see the detail of.</summary>
	public long PrivateContributions { get; set; }
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
