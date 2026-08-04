namespace BB84.GitHub.Statistics.Configuration;

/// <summary>
/// Every configurable value, settable by CLI flag, environment variable, or default.
/// </summary>
/// <remarks>
/// Mirrors the <c>Args</c> struct in the Zig implementation. Option names are
/// canonical snake_case: the CLI accepts <c>--access-token</c> or
/// <c>--access_token</c>, and the environment accepts <c>ACCESS_TOKEN</c>.
/// </remarks>
internal sealed class AppOptions
{
	public string? AccessToken { get; set; }

	public string? JsonInputFile { get; set; }

	public string? JsonOutputFile { get; set; }

	public bool Silent { get; set; }

	public bool Debug { get; set; }

	public bool Verbose { get; set; }

	public string? ExcludeRepos { get; set; }

	public string? ExcludeLangs { get; set; }

	public bool ExcludePrivate { get; set; }

	public string? OverviewOutputFile { get; set; }

	public string? LanguagesOutputFile { get; set; }

	public string? OverviewTemplate { get; set; }

	public string? LanguagesTemplate { get; set; }

	public string? OverviewFields { get; set; }

	public string? LanguagesFields { get; set; }

	public int? MaxRetries { get; set; } = 25;

	public bool Version { get; set; }

	public string? DumpOverviewTemplate { get; set; }

	public string? DumpLanguagesTemplate { get; set; }

	/// <summary>Separators for <c>--exclude-repos</c>; includes space.</summary>
	private const string RepoSeparators = " ,\t\r\n|\"'\0";

	/// <summary>
	/// Separators for <c>--exclude-langs</c>. Space is deliberately excluded so
	/// language names containing spaces ("Jupyter Notebook") survive splitting.
	/// </summary>
	private const string LangSeparators = ",\t\r\n|\"'\0";

	public IReadOnlyList<string> ExcludedRepoPatterns => SplitList(ExcludeRepos, RepoSeparators);

	public IReadOnlyList<string> ExcludedLangPatterns => SplitList(ExcludeLangs, LangSeparators);

	/// <summary>
	/// The requested overview rows, in order. Empty means "use the default set".
	/// Split like <c>--exclude-repos</c>, spaces included, since no field id
	/// contains one.
	/// </summary>
	public IReadOnlyList<string> OverviewFieldIds => SplitList(OverviewFields, RepoSeparators);

	/// <summary>
	/// The requested language legend spans, in order. Empty means "use the default
	/// set". Split like <see cref="OverviewFieldIds"/>: no field id contains a space.
	/// </summary>
	public IReadOnlyList<string> LanguagesFieldIds => SplitList(LanguagesFields, RepoSeparators);

	private static List<string> SplitList(string? value, string separators)
	{
		if (string.IsNullOrEmpty(value))
		{
			return [];
		}

		string[] parts = value.Split(separators.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
		List<string> result = new(parts.Length);
		foreach (string part in parts)
		{
			result.Add(part.Trim(' '));
		}

		return result;
	}
}
