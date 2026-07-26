using System.Globalization;

namespace BB84.GitHub.Statistics.Rendering;

/// <summary>One selectable row of the overview table.</summary>
/// <param name="Id">What the user types in <c>--overview-fields</c>.</param>
/// <param name="Label">The text in the left-hand cell.</param>
/// <param name="Icon">
/// The complete <c>&lt;svg&gt;</c> element preceding the label. The whole element
/// is stored rather than just the path data because the icons inherited from the
/// original template disagree about attribute order and about which of
/// <c>version</c>, <c>role</c> and <c>aria-hidden</c> they carry, and reproducing
/// them exactly is what keeps the default output byte-identical.
/// </param>
/// <param name="Value">Renders the right-hand cell.</param>
internal sealed record OverviewField(
		string Id,
		string Label,
		string Icon,
		Func<AggregateStats, string> Value);

/// <summary>
/// The catalogue of rows the overview template can show, and the parser for
/// <c>--overview-fields</c>.
/// </summary>
/// <remarks>
/// An explicit table of records with delegates, like
/// <c>OptionsParser.Options</c>. Nothing here is discovered by reflection, so it
/// survives NativeAOT trimming.
/// </remarks>
internal static class OverviewFields
{
	// -- Icons ------------------------------------------------------------------
	//
	// The first six are lifted verbatim from the original overview.svg, including
	// their inconsistent attribute order. New icons use the shorter form.

	private const string IconStar =
			"""<svg class="octicon" viewBox="0 0 16 16" xmlns="http://www.w3.org/2000/svg" version="1.1" width="16" height="16"><path fill-rule="evenodd" d="M8 .25a.75.75 0 01.673.418l1.882 3.815 4.21.612a.75.75 0 01.416 1.279l-3.046 2.97.719 4.192a.75.75 0 01-1.088.791L8 12.347l-3.766 1.98a.75.75 0 01-1.088-.79l.72-4.194L.818 6.374a.75.75 0 01.416-1.28l4.21-.611L7.327.668A.75.75 0 018 .25zm0 2.445L6.615 5.5a.75.75 0 01-.564.41l-3.097.45 2.24 2.184a.75.75 0 01.216.664l-.528 3.084 2.769-1.456a.75.75 0 01.698 0l2.77 1.456-.53-3.084a.75.75 0 01.216-.664l2.24-2.183-3.096-.45a.75.75 0 01-.564-.41L8 2.694v.001z"></path></svg>""";

	private const string IconFork =
			"""<svg class="octicon" viewBox="0 0 16 16" xmlns="http://www.w3.org/2000/svg" version="1.1" width="16" height="16" role="img"><path fill-rule="evenodd" d="M5 3.25a.75.75 0 11-1.5 0 .75.75 0 011.5 0zm0 2.122a2.25 2.25 0 10-1.5 0v.878A2.25 2.25 0 005.75 8.5h1.5v2.128a2.251 2.251 0 101.5 0V8.5h1.5a2.25 2.25 0 002.25-2.25v-.878a2.25 2.25 0 10-1.5 0v.878a.75.75 0 01-.75.75h-4.5A.75.75 0 015 6.25v-.878zm3.75 7.378a.75.75 0 11-1.5 0 .75.75 0 011.5 0zm3-8.75a.75.75 0 100-1.5.75.75 0 000 1.5z"></path></svg>""";

	private const string IconContributions =
			"""<svg class="octicon" viewBox="0 0 16 16" xmlns="http://www.w3.org/2000/svg" version="1.1" width="16" height="16" aria-hidden="true"><path fill-rule="evenodd" d="M1 2.5A2.5 2.5 0 013.5 0h8.75a.75.75 0 01.75.75v3.5a.75.75 0 01-1.5 0V1.5h-8a1 1 0 00-1 1v6.708A2.492 2.492 0 013.5 9h3.25a.75.75 0 010 1.5H3.5a1 1 0 100 2h5.75a.75.75 0 010 1.5H3.5A2.5 2.5 0 011 11.5v-9zm13.23 7.79a.75.75 0 001.06-1.06l-2.505-2.505a.75.75 0 00-1.06 0L9.22 9.229a.75.75 0 001.06 1.061l1.225-1.224v6.184a.75.75 0 001.5 0V9.066l1.224 1.224z"></path></svg>""";

	private const string IconDiff =
			"""<svg class="octicon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" width="16" height="16"><path fill-rule="evenodd" d="M8.75 1.75a.75.75 0 00-1.5 0V5H4a.75.75 0 000 1.5h3.25v3.25a.75.75 0 001.5 0V6.5H12A.75.75 0 0012 5H8.75V1.75zM4 13a.75.75 0 000 1.5h8a.75.75 0 100-1.5H4z"></path></svg>""";

	private const string IconEye =
			"""<svg class="octicon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" width="16" height="16"><path fill-rule="evenodd" d="M1.679 7.932c.412-.621 1.242-1.75 2.366-2.717C5.175 4.242 6.527 3.5 8 3.5c1.473 0 2.824.742 3.955 1.715 1.124.967 1.954 2.096 2.366 2.717a.119.119 0 010 .136c-.412.621-1.242 1.75-2.366 2.717C10.825 11.758 9.473 12.5 8 12.5c-1.473 0-2.824-.742-3.955-1.715C2.92 9.818 2.09 8.69 1.679 8.068a.119.119 0 010-.136zM8 2c-1.981 0-3.67.992-4.933 2.078C1.797 5.169.88 6.423.43 7.1a1.619 1.619 0 000 1.798c.45.678 1.367 1.932 2.637 3.024C4.329 13.008 6.019 14 8 14c1.981 0 3.67-.992 4.933-2.078 1.27-1.091 2.187-2.345 2.637-3.023a1.619 1.619 0 000-1.798c-.45-.678-1.367-1.932-2.637-3.023C11.671 2.992 9.981 2 8 2zm0 8a2 2 0 100-4 2 2 0 000 4z"></path></svg>""";

	private const string IconRepo =
			"""<svg class="octicon" viewBox="0 0 16 16" xmlns="http://www.w3.org/2000/svg" version="1.1" width="16" height="16" aria-hidden="true"><path fill-rule="evenodd" d="M2 2.5A2.5 2.5 0 014.5 0h8.75a.75.75 0 01.75.75v12.5a.75.75 0 01-.75.75h-2.5a.75.75 0 110-1.5h1.75v-2h-8a1 1 0 00-.714 1.7.75.75 0 01-1.072 1.05A2.495 2.495 0 012 11.5v-9zm10.5-1V9h-8c-.356 0-.694.074-1 .208V2.5a1 1 0 011-1h8zM5 12.25v3.25a.25.25 0 00.4.2l1.45-1.087a.25.25 0 01.3 0L8.6 15.7a.25.25 0 00.4-.2v-3.25a.25.25 0 00-.25-.25h-3.5a.25.25 0 00-.25.25z"></path></svg>""";

	private const string IconPerson =
			"""<svg class="octicon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" width="16" height="16"><path fill-rule="evenodd" d="M10.5 5a2.5 2.5 0 11-5 0 2.5 2.5 0 015 0zm.061 3.073a4 4 0 10-5.123 0 6.004 6.004 0 00-3.431 5.142.75.75 0 001.498.07 4.5 4.5 0 018.99 0 .75.75 0 101.498-.07 6.005 6.005 0 00-3.432-5.142z"></path></svg>""";

	private const string IconCommit =
			"""<svg class="octicon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" width="16" height="16"><path fill-rule="evenodd" d="M10.5 7.75a2.5 2.5 0 11-5 0 2.5 2.5 0 015 0zm1.43.75a4.002 4.002 0 01-7.86 0H.75a.75.75 0 110-1.5h3.32a4.001 4.001 0 017.86 0h3.32a.75.75 0 110 1.5h-3.32z"></path></svg>""";

	private const string IconPullRequest =
			"""<svg class="octicon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" width="16" height="16"><path fill-rule="evenodd" d="M7.177 3.073L9.573.677A.25.25 0 0110 .854v4.792a.25.25 0 01-.427.177L7.177 3.427a.25.25 0 010-.354zM3.75 2.5a.75.75 0 100 1.5.75.75 0 000-1.5zm-2.25.75a2.25 2.25 0 113 2.122v5.256a2.251 2.251 0 11-1.5 0V5.372A2.25 2.25 0 011.5 3.25zM11 2.5h-1V4h1a1 1 0 011 1v5.628a2.251 2.251 0 101.5 0V5A2.5 2.5 0 0011 2.5zm1 10.25a.75.75 0 111.5 0 .75.75 0 01-1.5 0zM3.75 12a.75.75 0 100 1.5.75.75 0 000-1.5z"></path></svg>""";

	private const string IconIssue =
			"""<svg class="octicon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" width="16" height="16"><path d="M8 9.5a1.5 1.5 0 100-3 1.5 1.5 0 000 3z"></path><path fill-rule="evenodd" d="M8 0a8 8 0 100 16A8 8 0 008 0zM1.5 8a6.5 6.5 0 1113 0 6.5 6.5 0 01-13 0z"></path></svg>""";

	private const string IconClock =
			"""<svg class="octicon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" width="16" height="16"><path fill-rule="evenodd" d="M8 3.5a.75.75 0 00-1.5 0v5c0 .414.336.75.75.75h4a.75.75 0 000-1.5H8v-4.25z"></path><path fill-rule="evenodd" d="M8 0a8 8 0 100 16A8 8 0 008 0zM1.5 8a6.5 6.5 0 1113 0 6.5 6.5 0 01-13 0z"></path></svg>""";

	/// <summary>
	/// The rows rendered when <c>--overview-fields</c> is absent. This exact list,
	/// in this exact order, is what the template shipped with before the row set
	/// became configurable.
	/// </summary>
	public static readonly IReadOnlyList<string> Default =
	[
		"stars",
		"forks",
		"contributions",
		"lines_changed",
		"views",
		"repos",
	];

	/// <summary>Every available row, in the order <c>--help</c> and the README list them.</summary>
	public static readonly IReadOnlyList<OverviewField> All =
	[
		// Repository totals, filtered by --exclude-repos / --exclude-private.
		new("stars", "Stars", IconStar,
					static s => SvgTemplate.FormatNumber(s.Stars)),
		new("forks", "Forks", IconFork,
					static s => SvgTemplate.FormatNumber(s.Forks)),
		new("contributions", "All-time contributions", IconContributions,
					static s => SvgTemplate.FormatNumber(s.Contributions)),
		new("lines_changed", "Lines of code changed", IconDiff,
					static s => SvgTemplate.FormatNumber(s.LinesChanged)),
		new("views", "Repository views (past two weeks)", IconEye,
					static s => SvgTemplate.FormatNumber(s.Views)),
		new("repos", "Repositories with contributions", IconRepo,
					static s => SvgTemplate.FormatNumber(s.Repos)),

		// The contribution total, broken apart. Costs no extra API calls.
		new("commits", "Commits", IconCommit,
					static s => SvgTemplate.FormatNumber(s.Breakdown.Commits)),
		new("prs", "Pull requests opened", IconPullRequest,
					static s => SvgTemplate.FormatNumber(s.Breakdown.Prs)),
		new("reviews", "Pull requests reviewed", IconPullRequest,
					static s => SvgTemplate.FormatNumber(s.Breakdown.Reviews)),
		new("issues", "Issues opened", IconIssue,
					static s => SvgTemplate.FormatNumber(s.Breakdown.Issues)),
		new("repos_created", "Repositories created", IconRepo,
					static s => SvgTemplate.FormatNumber(s.Breakdown.ReposCreated)),

		// Profile counters.
		new("followers", "Followers", IconPerson,
					static s => SvgTemplate.FormatNumber(s.Profile.Followers)),
		new("following", "Following", IconPerson,
					static s => SvgTemplate.FormatNumber(s.Profile.Following)),
		new("stars_given", "Stars given", IconStar,
					static s => SvgTemplate.FormatNumber(s.Profile.StarsGiven)),
		new("own_repos", "Repositories owned", IconRepo,
					static s => SvgTemplate.FormatNumber(s.Profile.OwnRepositories)),
		new("prs_merged", "Pull requests merged", IconPullRequest,
					static s => SvgTemplate.FormatNumber(s.Profile.MergedPullRequests)),
		new("gists", "Public gists", IconRepo,
					static s => SvgTemplate.FormatNumber(s.Profile.Gists)),
		new("organizations", "Organizations", IconRepo,
					static s => SvgTemplate.FormatNumber(s.Profile.Organizations)),
		new("watching", "Repositories watched", IconEye,
					static s => SvgTemplate.FormatNumber(s.Profile.Watching)),
		new("sponsors", "Sponsors", IconPerson,
					static s => SvgTemplate.FormatNumber(s.Profile.Sponsors)),
		new("disk_usage", "Repository disk usage", IconDiff,
					static s => FormatDiskUsage(s.Profile.DiskUsageKb)),
		new("account_age", "Account age", IconClock,
					static s => FormatAccountAge(s.Profile.CreatedAt)),
		new("member_since", "Member since", IconClock,
					static s => FormatMemberSince(s.Profile.CreatedAt)),

		// Calendar-derived figures.
		new("current_streak", "Current streak", IconClock,
					static s => FormatDays(s.Streaks.Current)),
		new("longest_streak", "Longest streak", IconClock,
					static s => FormatDays(s.Streaks.Longest)),
		new("contributions_this_year", "Contributions this year", IconContributions,
					static s => SvgTemplate.FormatNumber(s.Streaks.ThisYear)),
		new("busiest_day", "Busiest day", IconClock,
					static s => FormatBusiestDay(s.Streaks.BusiestDay, s.Streaks.BusiestDayCount)),
		new("private_contributions", "Private contributions", IconContributions,
					static s => SvgTemplate.FormatNumber(s.Streaks.Private)),
	];

	/// <summary>
	/// Looks up each id in <paramref name="ids"/>, preserving the caller's order.
	/// </summary>
	/// <exception cref="InvalidFieldException">
	/// An id is not in the catalogue, or is listed twice. Both are typos far more
	/// often than intent, and a silently dropped row is hard to notice in an image.
	/// </exception>
	public static IReadOnlyList<OverviewField> Resolve(IReadOnlyList<string> ids)
	{
		List<OverviewField> resolved = new(ids.Count);
		HashSet<string> seen = new(StringComparer.Ordinal);

		foreach (string id in ids)
		{
			OverviewField field = Find(id)
					?? throw new InvalidFieldException(id, $"Unknown overview field '{id}'. Valid fields: {Catalogue}.");

			if (!seen.Add(field.Id))
			{
				throw new InvalidFieldException(id, $"Overview field '{id}' is listed more than once.");
			}

			resolved.Add(field);
		}

		return resolved;
	}

	private static OverviewField? Find(string id)
	{
		foreach (OverviewField field in All)
		{
			if (string.Equals(field.Id, id, StringComparison.OrdinalIgnoreCase))
			{
				return field;
			}
		}

		return null;
	}

	private static string Catalogue => string.Join(", ", All.Select(static f => f.Id));

	// -- Formatters -------------------------------------------------------------
	//
	// All invariant. A culture-sensitive format here would rewrite every generated
	// SVG on a de-DE host, which is the trap SvgTemplate.FormatNumber documents.

	private const long KilobytesPerMegabyte = 1024;
	private const long KilobytesPerGigabyte = 1024 * 1024;

	/// <summary>Formats a size GitHub reports in kilobytes.</summary>
	internal static string FormatDiskUsage(long kilobytes) => kilobytes switch
	{
		>= KilobytesPerGigabyte => Fixed((double)kilobytes / KilobytesPerGigabyte, "GB"),
		>= KilobytesPerMegabyte => Fixed((double)kilobytes / KilobytesPerMegabyte, "MB"),
		_ => SvgTemplate.FormatNumber(kilobytes) + " KB",
	};

	private static string Fixed(double value, string unit) =>
			string.Create(CultureInfo.InvariantCulture, $"{value:F1} {unit}");

	/// <summary>Whole years since <paramref name="createdAt"/>, or months below a year.</summary>
	internal static string FormatAccountAge(string? createdAt)
	{
		if (!TryParseCreatedAt(createdAt, out DateTimeOffset created))
		{
			return "unknown";
		}

		DateTimeOffset now = DateTimeOffset.UtcNow;
		int months = ((now.Year - created.Year) * 12) + now.Month - created.Month;
		if (now.Day < created.Day)
		{
			months--;
		}

		months = Math.Max(months, 0);
		int years = months / 12;

		return years >= 1
				? string.Create(CultureInfo.InvariantCulture, $"{years} year{Plural(years)}")
				: string.Create(CultureInfo.InvariantCulture, $"{months} month{Plural(months)}");
	}

	internal static string FormatMemberSince(string? createdAt) =>
			TryParseCreatedAt(createdAt, out DateTimeOffset created)
					? created.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
					: "unknown";

	internal static string FormatDays(long days) =>
			string.Create(CultureInfo.InvariantCulture, $"{SvgTemplate.FormatNumber(days)} day{Plural(days)}");

	internal static string FormatBusiestDay(string? date, long count) =>
			date is null
					? "none"
					: string.Create(CultureInfo.InvariantCulture, $"{date} ({SvgTemplate.FormatNumber(count)})");

	private static bool TryParseCreatedAt(string? value, out DateTimeOffset created)
	{
		if (string.IsNullOrEmpty(value))
		{
			created = default;
			return false;
		}

		return DateTimeOffset.TryParse(
				value,
				CultureInfo.InvariantCulture,
				DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
				out created);
	}

	private static string Plural(long count) => count == 1 ? string.Empty : "s";
}
