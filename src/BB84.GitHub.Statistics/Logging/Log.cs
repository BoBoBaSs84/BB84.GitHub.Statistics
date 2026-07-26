using Microsoft.Extensions.Logging;
using System.Net;

namespace BB84.GitHub.Statistics.Logging;

/// <summary>
/// Every log message the application emits, as compile-time generated methods.
/// </summary>
/// <remarks>
/// <para>
/// The <c>[LoggerMessage]</c> source generator emits a cached delegate and an
/// <c>IsEnabled</c> guard per message, which avoids the boxing and
/// <c>params object?[]</c> allocation of the <c>ILogger.LogX</c> extension
/// methods (CA1848) and stops arguments being formatted when the level is
/// disabled (CA1873). It is also fully NativeAOT friendly.
/// </para>
/// <para>
/// The logger is passed explicitly rather than discovered on a containing type,
/// because the classes that log capture their <see cref="ILogger"/> through a
/// primary constructor parameter, which the generator cannot see.
/// </para>
/// <para>
/// Message text is byte-for-byte what the previous implementation emitted, so
/// CI logs stay comparable across the rewrite.
/// </para>
/// </remarks>
internal static partial class Log
{
	/// <summary>
	/// Suffix for messages that read naturally in both singular and plural,
	/// preserved from the original implementation's wording.
	/// </summary>
	public static string Plural(long count) => count == 1 ? string.Empty : "s";

	// -- Program --------------------------------------------------------------

	[LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Reading data from '{Path}'")]
	public static partial void ReadingData(ILogger logger, string path);

	[LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Writing data to '{Path}'")]
	public static partial void WritingData(ILogger logger, string path);

	[LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Collecting statistics from GitHub API")]
	public static partial void CollectingStatistics(ILogger logger);

	[LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "{Reason}")]
	public static partial void Failed(ILogger logger, string reason);

	// -- GitHubApiClient ------------------------------------------------------

	[LoggerMessage(
		EventId = 10,
		Level = LogLevel.Information,
		Message = "Skipping {Context} due to an invalid response from GitHub: {Reason}")]
	public static partial void SkippingInvalidResponse(ILogger logger, string context, string reason);

	[LoggerMessage(
		EventId = 11,
		Level = LogLevel.Debug,
		Message = "Request failed ({Reason}); retrying {Attempt}/{Retries}.")]
	public static partial void RequestRetrying(ILogger logger, string reason, int attempt, int retries);

	// -- GitCli ---------------------------------------------------------------

	[LoggerMessage(EventId = 20, Level = LogLevel.Debug, Message = "git {Command} exited {ExitCode}: {Error}")]
	public static partial void GitCommandFailed(ILogger logger, string command, int exitCode, string error);

	[LoggerMessage(
		EventId = 21,
		Level = LogLevel.Debug,
		Message = "Could not remove the temporary clone at {Path}: {Reason}")]
	public static partial void TemporaryCloneNotRemoved(ILogger logger, string path, string reason);

	// -- LinesChangedResolver -------------------------------------------------

	[LoggerMessage(
		EventId = 30,
		Level = LogLevel.Debug,
		Message = "Trying to get lines of code changed for {Repository}...")]
	public static partial void TryingLinesChanged(ILogger logger, string repository);

	[LoggerMessage(
		EventId = 31,
		Level = LogLevel.Information,
		Message = "Got {Lines} line{Plural} changed by {User} in {Repository}")]
	public static partial void GotLinesChanged(
		ILogger logger,
		uint lines,
		string plural,
		string user,
		string repository);

	[LoggerMessage(
		EventId = 32,
		Level = LogLevel.Debug,
		Message = "Sleeping for {Delay}s. Waiting for {Count} repo{Plural}.")]
	public static partial void SleepingBeforeRetry(ILogger logger, long delay, int count, string plural);

	[LoggerMessage(
		EventId = 33,
		Level = LogLevel.Information,
		Message = "Failed to get contribution data for {Repository} ({Status})")]
	public static partial void ContributionDataFailed(ILogger logger, string repository, HttpStatusCode status);

	[LoggerMessage(EventId = 34, Level = LogLevel.Information, Message = "Cloning {Repository} to get lines changed...")]
	public static partial void CloningRepository(ILogger logger, string repository);

	// -- StatisticsCollector --------------------------------------------------

	[LoggerMessage(EventId = 40, Level = LogLevel.Information, Message = "Getting contribution years...")]
	public static partial void GettingContributionYears(ILogger logger);

	[LoggerMessage(EventId = 41, Level = LogLevel.Error, Message = "Failed to get contribution years ({Status})")]
	public static partial void ContributionYearsFailed(ILogger logger, HttpStatusCode status);

	[LoggerMessage(EventId = 42, Level = LogLevel.Information, Message = "Getting contributor emails...")]
	public static partial void GettingContributorEmails(ILogger logger);

	[LoggerMessage(
		EventId = 43,
		Level = LogLevel.Error,
		Message = "Failed to get user emails. Token may be missing `user:email` permission.")]
	public static partial void UserEmailsFailed(ILogger logger);

	[LoggerMessage(EventId = 44, Level = LogLevel.Information, Message = "Getting data for {Name} ({User})...")]
	public static partial void GettingDataForNamedUser(ILogger logger, string name, string user);

	[LoggerMessage(EventId = 45, Level = LogLevel.Information, Message = "Getting data for user {User}...")]
	public static partial void GettingDataForUser(ILogger logger, string user);

	[LoggerMessage(
		EventId = 46,
		Level = LogLevel.Information,
		Message = "Getting {Months} month{Plural} of data starting from {Month}/{Year}...")]
	public static partial void GettingRange(ILogger logger, int months, string plural, int month, int year);

	[LoggerMessage(EventId = 47, Level = LogLevel.Error, Message = "Failed to get data from {Year} ({Status})")]
	public static partial void RangeFailed(ILogger logger, int year, HttpStatusCode status);

	[LoggerMessage(EventId = 48, Level = LogLevel.Information, Message = "Parsed {Count} total repositories from {Year}")]
	public static partial void ParsedRepositories(ILogger logger, int count, int year);

	[LoggerMessage(
		EventId = 49,
		Level = LogLevel.Warning,
		Message = "More than {Limit} repos returned for {Month}/{Year}. Some data may be omitted due to GitHub API limitations.")]
	public static partial void RepositoryLimitReached(ILogger logger, int limit, int month, int year);

	[LoggerMessage(EventId = 50, Level = LogLevel.Debug, Message = "Skipping {Repository} (seen)")]
	public static partial void SkippingSeenRepository(ILogger logger, string repository);

	[LoggerMessage(EventId = 51, Level = LogLevel.Information, Message = "Getting views for {Repository}...")]
	public static partial void GettingViews(ILogger logger, string repository);

	[LoggerMessage(
		EventId = 52,
		Level = LogLevel.Information,
		Message = "Failed to get views for {Repository} ({Status})")]
	public static partial void ViewsFailed(ILogger logger, string repository, HttpStatusCode status);

	[LoggerMessage(
		EventId = 53,
		Level = LogLevel.Debug,
		Message = "GitHub exceeded its resource limits over {Months} month(s) from {Month}/{Year}; asking for less at a time.")]
	public static partial void ResourceLimitSubdividing(ILogger logger, int months, int month, int year);

	[LoggerMessage(
		EventId = 54,
		Level = LogLevel.Debug,
		Message = "Retrying {Month}/{Year} after exceeding resource limits ({Attempt}/{Retries}).")]
	public static partial void ResourceLimitRetrying(ILogger logger, int attempt, int retries, int month, int year);

	[LoggerMessage(
		EventId = 55,
		Level = LogLevel.Warning,
		Message = "GitHub still exceeded its resource limits for {Month}/{Year}, which cannot be narrowed further. Some data may be omitted.")]
	public static partial void ResourceLimitReached(ILogger logger, int month, int year);

	[LoggerMessage(EventId = 56, Level = LogLevel.Information, Message = "Getting profile counters...")]
	public static partial void GettingProfile(ILogger logger);

	[LoggerMessage(
		EventId = 57,
		Level = LogLevel.Information,
		Message = "Failed to get profile counters ({Status}); they will be reported as zero.")]
	public static partial void ProfileFailed(ILogger logger, HttpStatusCode status);

	[LoggerMessage(EventId = 58, Level = LogLevel.Information, Message = "Getting contribution calendar for {Year}...")]
	public static partial void GettingCalendar(ILogger logger, int year);

	[LoggerMessage(
		EventId = 59,
		Level = LogLevel.Information,
		Message = "Failed to get the contribution calendar for {Year} ({Status}); streaks may be understated.")]
	public static partial void CalendarFailed(ILogger logger, int year, HttpStatusCode status);
}
