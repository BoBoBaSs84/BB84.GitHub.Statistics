using System.Text.Json.Serialization;

namespace BB84.GitHub.Statistics.GitHub.Models;

internal sealed class UserEmail
{
	[JsonPropertyName("email")]
	public string Email { get; set; } = string.Empty;
}

internal sealed class TrafficViews
{
	[JsonPropertyName("count")]
	public uint Count { get; set; }
}

internal sealed class ContributorStats
{
	[JsonPropertyName("author")]
	public ContributorAuthor? Author { get; set; }

	[JsonPropertyName("weeks")]
	public List<ContributorWeek> Weeks { get; set; } = [];
}

internal sealed class ContributorAuthor
{
	[JsonPropertyName("login")]
	public string Login { get; set; } = string.Empty;
}

internal sealed class ContributorWeek
{
	/// <summary>Additions.</summary>
	[JsonPropertyName("a")]
	public uint Additions { get; set; }

	/// <summary>Deletions.</summary>
	[JsonPropertyName("d")]
	public uint Deletions { get; set; }
}
