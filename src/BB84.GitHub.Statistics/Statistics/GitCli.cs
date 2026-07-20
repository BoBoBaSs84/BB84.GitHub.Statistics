using BB84.GitHub.Statistics.Logging;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;

namespace BB84.GitHub.Statistics.Statistics;

/// <summary>
/// Computes lines changed by cloning a repository and tallying <c>git log --numstat</c>.
/// </summary>
/// <remarks>
/// This is the fallback for GitHub's contributor-statistics endpoint, which has
/// been returning 202 indefinitely for many repositories. It under-counts
/// relative to GitHub's own numbers because it cannot attribute squashed
/// multi-author pull requests, but it produces an answer where the API does not.
/// </remarks>
internal class GitCli(ILogger<GitCli> logger)
{
	private bool? _isInstalled;

	public async Task<bool> IsInstalledAsync(CancellationToken ct)
	{
		if (_isInstalled is { } cached)
		{
			return cached;
		}

		try
		{
			ProcessResult result = await RunAsync(null, ["--version"], ct).ConfigureAwait(false);
			_isInstalled = result.ExitCode == 0;
		}
		catch (Exception e) when (e is System.ComponentModel.Win32Exception or FileNotFoundException)
		{
			_isInstalled = false;
		}

		return _isInstalled.Value;
	}

	/// <summary>
	/// Clones <paramref name="repo"/> into a temporary directory and sums additions
	/// and deletions across commits authored by any of <paramref name="emails"/>.
	/// Returns 0 when git is unavailable.
	/// </summary>
	public virtual async Task<uint> GetLinesChangedAsync(
			string login,
			string token,
			string repo,
			IReadOnlyList<string> emails,
			CancellationToken ct)
	{
		if (!await IsInstalledAsync(ct).ConfigureAwait(false))
		{
			return 0;
		}

		string workDir = Path.Combine(Path.GetTempPath(), "github-stats-" + Guid.NewGuid().ToString("N"));
		string repoPath = Path.Combine(workDir, repo.Replace('/', '_'));
		_ = Directory.CreateDirectory(workDir);

		try
		{
			// The URL embeds the token, so it must never reach a log sink.
			string url = $"https://{login}:{token}@github.com/{repo}.git";
			ProcessResult clone = await RunAsync(
					null,
					["clone", "--bare", "--filter=blob:limit=1m", "--no-tags", "--single-branch", url, repoPath],
					ct).ConfigureAwait(false);

			if (clone.ExitCode != 0)
			{
				throw new InvalidOperationException($"Failed to clone {repo}.");
			}

			List<string> args = ["-C", repoPath, "log", "--numstat", "--pretty=tformat:"];
			foreach (string email in emails)
			{
				args.Add("--author");
				args.Add(email);
			}

			ProcessResult log = await RunAsync(null, args, ct).ConfigureAwait(false);
			if (log.ExitCode != 0)
			{
				throw new InvalidOperationException($"Failed to read the log for {repo}.");
			}

			return SumNumstat(log.StandardOutput);
		}
		finally
		{
			TryDeleteTree(workDir);
		}
	}

	/// <summary>
	/// Sums the first two whitespace-delimited fields of each line. Binary files
	/// report <c>-</c> for both counts, which parses as zero.
	/// </summary>
	internal static uint SumNumstat(string numstat)
	{
		uint total = 0;
		Span<Range> fields = stackalloc Range[3];

		foreach (ReadOnlySpan<char> line in numstat.AsSpan().EnumerateLines())
		{
			if (line.IsEmpty)
			{
				continue;
			}

			int count = line.SplitAny(fields, " \t", StringSplitOptions.RemoveEmptyEntries);
			if (count < 2)
			{
				continue;
			}

			total += ParseOrZero(line[fields[0]]);
			total += ParseOrZero(line[fields[1]]);
		}

		return total;
	}

	private static uint ParseOrZero(ReadOnlySpan<char> value) =>
			uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out uint parsed) ? parsed : 0;

	private readonly record struct ProcessResult(int ExitCode, string StandardOutput);

	private async Task<ProcessResult> RunAsync(
			string? workingDirectory,
			List<string> arguments,
			CancellationToken ct)
	{
		ProcessStartInfo startInfo = new()
		{
			FileName = "git",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
		};

		// Arguments are added individually so nothing is re-parsed by a shell.
		foreach (string argument in arguments)
		{
			startInfo.ArgumentList.Add(argument);
		}

		using Process process = new() { StartInfo = startInfo };
		_ = process.Start();

		Task<string> stdout = process.StandardOutput.ReadToEndAsync(ct);
		Task<string> stderr = process.StandardError.ReadToEndAsync(ct);
		_ = await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
		await process.WaitForExitAsync(ct).ConfigureAwait(false);

		if (process.ExitCode != 0)
		{
			Log.GitCommandFailed(logger, arguments[0], process.ExitCode, stderr.Result);
		}

		return new ProcessResult(process.ExitCode, stdout.Result);
	}

	private void TryDeleteTree(string path)
	{
		try
		{
			if (Directory.Exists(path))
			{
				// Bare clones contain read-only pack files on Windows.
				foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
				{
					File.SetAttributes(file, FileAttributes.Normal);
				}

				Directory.Delete(path, recursive: true);
			}
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			Log.TemporaryCloneNotRemoved(logger, path, e.Message);
		}
	}
}
