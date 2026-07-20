namespace BB84.GitHub.Statistics.Matching;

/// <summary>
/// Recursive-backtracking glob matching. Potentially very slow if there are a
/// lot of globs. Good enough for now, though. (If it's good enough for the GNU
/// glob function, it's good enough for me.)
/// </summary>
/// <remarks>
/// <para>
/// Only <c>*</c> is special. <c>?</c> is a literal, and <c>*</c> spans <c>/</c>
/// rather than stopping at path separators the way shell globbing does. This is
/// deliberately not <see cref="System.IO.Enumeration.FileSystemName.MatchesSimpleExpression"/>,
/// which honours <c>?</c> and would silently change which repositories existing
/// <c>EXCLUDE_REPOS</c> settings match.
/// </para>
/// <para>Max recursion depth is the number of stars in the pattern plus one.</para>
/// </remarks>
internal static class Glob
{
	public static bool Match(ReadOnlySpan<char> pattern, ReadOnlySpan<char> s)
	{
		int starOffset = pattern.IndexOf('*');
		if (starOffset < 0)
		{
			return AsciiEqualsIgnoreCase(pattern, s);
		}

		if (starOffset > s.Length ||
				!AsciiEqualsIgnoreCase(s[..starOffset], pattern[..starOffset]))
		{
			return false;
		}

		ReadOnlySpan<char> rest = pattern[(starOffset + 1)..];
		for (int globEnd = 0; globEnd <= s.Length; globEnd++)
		{
			if (Match(rest, s[globEnd..]))
			{
				return true;
			}
		}

		return false;
	}

	public static bool MatchAny(IReadOnlyList<string> patterns, ReadOnlySpan<char> s)
	{
		for (int i = 0; i < patterns.Count; i++)
		{
			if (Match(patterns[i], s))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// ASCII-only case-insensitive comparison, matching Zig's <c>std.ascii.eqlIgnoreCase</c>.
	/// Ordinal comparison would additionally case-fold non-ASCII characters, which
	/// would be a behaviour change however unlikely to be observable on repo names.
	/// </summary>
	private static bool AsciiEqualsIgnoreCase(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
	{
		if (a.Length != b.Length)
		{
			return false;
		}

		for (int i = 0; i < a.Length; i++)
		{
			if (ToLowerAscii(a[i]) != ToLowerAscii(b[i]))
			{
				return false;
			}
		}

		return true;
	}

	private static char ToLowerAscii(char c) => (uint)(c - 'A') <= 'Z' - 'A' ? (char)(c | 0x20) : c;
}
