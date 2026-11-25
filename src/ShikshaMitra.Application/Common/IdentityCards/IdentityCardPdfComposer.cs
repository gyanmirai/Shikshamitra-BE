using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ShikshaMitra.Common.IdentityCards;

internal sealed record IdentityCardArtwork(string FrontSvg, string? BackSvg);

internal static class IdentityCardPdfComposer
{
	public static byte[] Compose(IdentityCardLayout layout, IdentityCardPrintSettings settings, IReadOnlyCollection<IdentityCardArtwork> cards, bool backEnabled)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var sheet = IdentityCardGeometry.Sheet(layout, settings);
		if (settings.SideMode == "back" && !backEnabled) throw IdentityCardGeometry.Invalid("Enable and configure the back before selecting back-only output.");
		return Document.Create(document =>
		{
			foreach (var chunk in cards.Chunk(settings.PageSize == "Card" ? 1 : sheet.EffectiveCardsPerPage))
			{
				foreach (var back in Sides(settings, backEnabled))
				{
					var width = settings.PageSize == "Card" ? sheet.CardWidthMm + 2 * settings.BleedMm : 210;
					var height = settings.PageSize == "Card" ? sheet.CardHeightMm + 2 * settings.BleedMm : 297;
					var artwork = BuildPageSvg(layout, settings, sheet, chunk, back, width, height);
					document.Page(page =>
					{
						page.Size(width, height, Unit.Millimetre);
						page.Margin(0);
						page.Content().Svg(artwork);
					});
				}
			}
		}).WithSettings(new DocumentSettings { ImageRasterDpi = 2540 }).GeneratePdf();
	}

	private static IEnumerable<bool> Sides(IdentityCardPrintSettings settings, bool backEnabled)
	{
		if (settings.SideMode != "back") yield return false;
		if (settings.SideMode != "front" && backEnabled) yield return true;
	}

	internal static string BuildPageSvg(IdentityCardLayout layout, IdentityCardPrintSettings settings, IdentityCardSheet sheet,
		IReadOnlyList<IdentityCardArtwork> cards, bool back, float width = 210, float height = 297)
	{
		var builder = new StringBuilder(FormattableString.Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}mm\" height=\"{height}mm\" viewBox=\"0 0 {width} {height}\">"));
		for (var index = 0; index < cards.Count; index++)
		{
			var (x, y) = settings.PageSize == "Card" ? (settings.BleedMm, settings.BleedMm) : sheet.Position(index, settings, back);
			if (settings.BleedMm > 0)
				builder.Append(FormattableString.Invariant($"<rect x=\"{x - settings.BleedMm}\" y=\"{y - settings.BleedMm}\" width=\"{sheet.SlotWidthMm}\" height=\"{sheet.SlotHeightMm}\" fill=\"{IdentityCardSvgFormatting.Escape(layout.BackgroundColor)}\"/>"));
			builder.Append(FormattableString.Invariant($"<g transform=\"translate({x} {y})\">"));
			if (back && settings.BackRotation == 180) builder.Append(FormattableString.Invariant($"<g transform=\"translate({sheet.CardWidthMm} {sheet.CardHeightMm}) rotate(180)\">"));
			builder.Append(FormattableString.Invariant($"<g transform=\"scale({sheet.CardWidthMm / layout.Width} {sheet.CardHeightMm / layout.Height})\">"));
			builder.Append(back ? cards[index].BackSvg : cards[index].FrontSvg).Append("</g>");
			if (back && settings.BackRotation == 180) builder.Append("</g>");
			builder.Append("</g>");
			if (settings.ShowCutMarks && settings.PageSize == "A4") AppendCutMarks(builder, x, y, sheet, settings, width, height);
		}
		return builder.Append("</svg>").ToString();
	}

	private static void AppendCutMarks(StringBuilder builder, float x, float y, IdentityCardSheet sheet, IdentityCardPrintSettings settings, float width, float height)
	{
		const float stroke = 0.1f;
		var start = settings.BleedMm + 0.2f;
		var minimumReach = start + 0.1f - 0.00001f;
		float Reach(float space) => settings.BleedMm + Math.Min(1.2f, Math.Min(settings.Gap / 2 - 0.2f, space - settings.BleedMm - stroke / 2));
		foreach (var dx in new[] { -1, 1 })
		foreach (var dy in new[] { -1, 1 })
		{
			var edgeX = dx < 0 ? x : x + sheet.CardWidthMm;
			var edgeY = dy < 0 ? y : y + sheet.CardHeightMm;
			var horizontal = Reach(dx < 0 ? edgeX : width - edgeX);
			var vertical = Reach(dy < 0 ? edgeY : height - edgeY);
			if (horizontal >= minimumReach) Line(edgeX + dx * start, edgeY, edgeX + dx * horizontal, edgeY);
			if (vertical >= minimumReach) Line(edgeX, edgeY + dy * start, edgeX, edgeY + dy * vertical);
		}
		void Line(float x1, float y1, float x2, float y2) =>
			builder.Append(FormattableString.Invariant($"<line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"#333333\" stroke-width=\"{stroke}\"/>"));
	}
}
