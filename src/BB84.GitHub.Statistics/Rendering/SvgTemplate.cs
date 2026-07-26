using System.Globalization;
using System.Text;

namespace BB84.GitHub.Statistics.Rendering;

/// <summary>
/// Thrown when a template references a placeholder with no supplied value, or
/// when <c>--overview-fields</c> names a row that does not exist.
/// </summary>
internal sealed class InvalidFieldException : Exception
{
	public InvalidFieldException(string field)
			: base($"Template references unknown field '{field}'.")
			=> Field = field;

	/// <summary>For callers that can explain the problem better than the default wording.</summary>
	public InvalidFieldException(string field, string message)
			: base(message)
			=> Field = field;

	public string Field { get; }
}

/// <summary>
/// Minimal <c>{{ field }}</c> substitution over a set of named values.
/// </summary>
/// <remarks>
/// Templates may be supplied at runtime via <c>--overview-template</c>, so an
/// unknown placeholder is a runtime failure rather than a compile-time one.
/// An unterminated <c>{{</c> is emitted literally.
/// </remarks>
internal static class SvgTemplate
{
	public static string Fill(string template, IReadOnlyDictionary<string, string> values)
	{
		StringBuilder result = new(template.Length * 2);

		int i = 0;
		while (i < template.Length)
		{
			int start = template.IndexOf("{{", i, StringComparison.Ordinal);
			if (start < 0)
			{
				_ = result.Append(template, i, template.Length - i);
				break;
			}

			int end = template.IndexOf("}}", start + 2, StringComparison.Ordinal);
			if (end < 0)
			{
				// No closing }}, so treat the initial {{ as a literal.
				_ = result.Append(template, i, template.Length - i);
				break;
			}

			_ = result.Append(template, i, start - i);

			// Zig trims spaces only, not arbitrary whitespace.
			string name = template[(start + 2)..end].Trim(' ');
			if (!values.TryGetValue(name, out string? value))
			{
				throw new InvalidFieldException(name);
			}

			_ = result.Append(value);
			i = end + 2;
		}

		return result.ToString();
	}

	/// <summary>
	/// Formats an integer with comma thousands separators, matching Zig's
	/// <c>decimalToString</c>. Pinned to the invariant culture so a de-DE host
	/// cannot render <c>14.361.640</c> where every existing SVG has <c>14,361,640</c>.
	/// </summary>
	public static string FormatNumber(long value) =>
			value.ToString("N0", CultureInfo.InvariantCulture);
}
