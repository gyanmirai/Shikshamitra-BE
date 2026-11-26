using System;
using System.Linq;
using System.Text;

namespace ShikshaMitra.Common.IdentityCards;

internal static class IdentityCardSheetTrimGuides
{
	private const float MarkLengthMm = 3;
	private const float MarkClearanceMm = 0.5f;
	private const float PaperEdgeInsetMm = 3;

	public static string BuildSvg(IdentityCardSheet sheet, IdentityCardPrintSettings settings, int count, bool back)
	{
		var svg = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"210mm\" height=\"297mm\" viewBox=\"0 0 210 297\">");
		AppendTo(svg, sheet, settings, count, back);
		return svg.Append("</svg>").ToString();
	}

	public static void AppendTo(StringBuilder svg, IdentityCardSheet sheet, IdentityCardPrintSettings settings, int count, bool back)
	{
		var positions = Enumerable.Range(0, count).Select(index => sheet.Position(index, settings, back)).ToArray();
		if (positions.Length == 0) return;

		var horizontal = Edges(positions.Select(p => p.X), sheet.CardWidthMm);
		var vertical = Edges(positions.Select(p => p.Y), sheet.CardHeightMm);
		var clearance = settings.BleedMm + MarkClearanceMm;
		var top = vertical[0] - clearance;
		var bottom = vertical[^1] + clearance;
		var left = horizontal[0] - clearance;
		var right = horizontal[^1] + clearance;

		svg.Append("<g stroke=\"#242424\" stroke-width=\"0.22\">");
		foreach (var x in horizontal)
		{
			AppendLine(svg, x, Math.Max(PaperEdgeInsetMm, top - MarkLengthMm), x, top);
			AppendLine(svg, x, bottom, x, Math.Min(297 - PaperEdgeInsetMm, bottom + MarkLengthMm));
		}
		foreach (var y in vertical)
		{
			AppendLine(svg, Math.Max(PaperEdgeInsetMm, left - MarkLengthMm), y, left, y);
			AppendLine(svg, right, y, Math.Min(210 - PaperEdgeInsetMm, right + MarkLengthMm), y);
		}
		svg.Append("</g>");
	}

	private static float[] Edges(System.Collections.Generic.IEnumerable<float> starts, float size) =>
		starts.SelectMany(start => new[] { start, start + size })
			.Select(edge => MathF.Round(edge, 4)).Distinct().Order().ToArray();

	private static void AppendLine(StringBuilder svg, float x1, float y1, float x2, float y2)
	{
		if (x2 - x1 + y2 - y1 < 0.2f) return;
		svg.Append(FormattableString.Invariant($"<line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\"/>"));
	}
}
