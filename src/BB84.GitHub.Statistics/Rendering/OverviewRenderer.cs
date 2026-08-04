using System.Globalization;
using System.Text;

namespace BB84.GitHub.Statistics.Rendering;

/// <summary>The pieces of <c>overview.svg</c> that depend on how many rows were selected.</summary>
/// <param name="Rows">The <c>&lt;tr&gt;</c> markup for the table body.</param>
/// <param name="Height">Height of the root <c>&lt;svg&gt;</c>.</param>
/// <param name="InnerHeight">Height of the <c>&lt;foreignObject&gt;</c>.</param>
internal readonly record struct OverviewMarkup(string Rows, int Height, int InnerHeight);

/// <summary>
/// Builds the table body injected into <c>overview.svg</c>, the same way
/// <see cref="LanguagesRenderer"/> builds the language legend.
/// </summary>
/// <remarks>
/// Newlines are hard-coded to <c>\n</c> rather than
/// <see cref="Environment.NewLine"/> so Windows builds produce identical SVGs.
/// The blank line between rows, and the ones after <c>&lt;tbody&gt;</c> and
/// before <c>&lt;/tbody&gt;</c>, are reproduced deliberately: with the default
/// field set the output has to match the template that shipped before the row
/// set became configurable, byte for byte.
/// </remarks>
internal static class OverviewRenderer
{
	/// <summary>
	/// Vertical space taken by one row: a 12px cell at an 18px line height, plus
	/// 0.25em of padding above and below.
	/// </summary>
	private const int RowHeight = 24;

	/// <summary>
	/// Everything that is not a row: the border, the header, and the padding
	/// around the <c>&lt;foreignObject&gt;</c>. Chosen so that the six default
	/// rows still come to the 210px the template has always been.
	/// </summary>
	private const int ChromeHeight = 66;

	/// <summary>The <c>&lt;foreignObject&gt;</c> inset, 21px of padding at each end.</summary>
	private const int InnerInset = 42;

	/// <summary>Matches the stagger the original six hand-written rows used.</summary>
	private const int AnimationStepMs = 150;

	public static OverviewMarkup Render(IReadOnlyList<OverviewField> fields, AggregateStats stats)
	{
		StringBuilder rows = new();

		_ = rows.Append('\n');

		for (int i = 0; i < fields.Count; i++)
		{
			OverviewField field = fields[i];

			if (i > 0)
			{
				_ = rows.Append("\n\n");
			}

			_ = rows.Append("<tr");

			// The first row animates immediately and carries no style attribute.
			if (i > 0)
			{
				_ = rows.Append(" style=\"animation-delay: ")
						.Append(((long)i * AnimationStepMs).ToString(CultureInfo.InvariantCulture))
						.Append("ms\"");
			}

			_ = rows.Append("><td>")
					.Append(field.Icon)
					.Append(SvgTemplate.Escape(field.Label))
					.Append("</td><td>")
					.Append(SvgTemplate.Escape(field.Value(stats)))
					.Append("</td></tr>");
		}

		_ = rows.Append('\n');

		int height = ChromeHeight + (RowHeight * fields.Count);

		return new OverviewMarkup(rows.ToString(), height, height - InnerInset);
	}
}
