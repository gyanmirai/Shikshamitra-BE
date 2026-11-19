using System;
using Volo.Abp;

namespace ShikshaMitra.Common.IdentityCards;

internal sealed record IdentityCardSheet(int Columns, int Rows, int Capacity, int EffectiveCardsPerPage,
	float CardWidthMm, float CardHeightMm, float StartXmm, float StartYmm, float SlotWidthMm, float SlotHeightMm)
{
	public (float X, float Y) Position(int index, IdentityCardPrintSettings settings, bool back)
	{
		var column = index % Columns;
		var row = index / Columns;
		if (back && settings.DuplexFlip == "long-edge") column = Columns - 1 - column;
		if (back && settings.DuplexFlip == "short-edge") row = Rows - 1 - row;
		return (StartXmm + column * (SlotWidthMm + settings.Gap) + settings.BleedMm + (back ? settings.BackOffsetXmm : 0),
			StartYmm + row * (SlotHeightMm + settings.Gap) + settings.BleedMm + (back ? settings.BackOffsetYmm : 0));
	}
}

internal static class IdentityCardGeometry
{
	public const string InvalidConfiguration = IdentityCardErrorCodes.InvalidConfiguration;
	public const float PointsPerMm = 72f / 25.4f;

	public static (float Width, float Height) PhysicalSize(IdentityCardLayout layout)
	{
		var vertical = layout.Orientation == "Vertical";
		return (layout.WidthMm ?? (vertical ? 53.98f : 85.6f), layout.HeightMm ?? (vertical ? 85.6f : 53.98f));
	}

	public static IdentityCardSheet Sheet(IdentityCardLayout layout, IdentityCardPrintSettings settings)
	{
		ValidateSettings(settings);
		var (width, height) = ValidatePhysicalSize(layout);
		if (settings.SafeInsetMm * 2 >= Math.Min(width, height)) throw Invalid("The card dimensions must contain the safe inset.");
		var slotWidth = width + settings.BleedMm * 2;
		var slotHeight = height + settings.BleedMm * 2;
		if (settings.PageSize == "Card")
		{
			if (settings.BackOffsetXmm != 0 || settings.BackOffsetYmm != 0) throw Invalid("Back offsets require A4 sheet output.");
			return new(1, 1, 1, 1, width, height, 0, 0, slotWidth, slotHeight);
		}
		var columns = (int)Math.Floor((210 - 2 * settings.Margin + settings.Gap) / (slotWidth + settings.Gap));
		var rows = (int)Math.Floor((297 - 2 * settings.Margin + settings.Gap) / (slotHeight + settings.Gap));
		if (columns < 1 || rows < 1) throw Invalid("No card fits A4 at the requested dimensions, bleed and margins.");
		var startX = (210 - columns * slotWidth - (columns - 1) * settings.Gap) / 2;
		var startY = (297 - rows * slotHeight - (rows - 1) * settings.Gap) / 2;
		if (Math.Abs(settings.BackOffsetXmm) > startX || Math.Abs(settings.BackOffsetYmm) > startY)
			throw Invalid("Back offsets would clip the sheet artwork.");
		return new(columns, rows, columns * rows, Math.Min(settings.CardsPerPage, columns * rows), width, height, startX, startY, slotWidth, slotHeight);
	}

	public static void ValidateSettings(IdentityCardPrintSettings settings)
	{
		if (settings.PageSize is not ("A4" or "Card") || settings.Units != "mm" || settings.CardsPerPage is < 1 or > 10 ||
			settings.DuplexFlip is not ("long-edge" or "short-edge") || settings.BackRotation is not (0 or 180) ||
			settings.SideMode is not ("both" or "front" or "back") || !InRange(settings.Margin, 0, 60) ||
			!InRange(settings.Gap, 0, 40) || !InRange(settings.BleedMm, 0, 10) || !InRange(settings.SafeInsetMm, 0, 20) ||
			!InRange(settings.BackOffsetXmm, -30, 30) || !InRange(settings.BackOffsetYmm, -30, 30))
			throw Invalid("Invalid print settings. Use millimetres, supported sides and duplex options, and valid ranges.");
	}

	private static bool InRange(float value, float minimum, float maximum) => float.IsFinite(value) && value >= minimum && value <= maximum;
	public static (float Width, float Height) ValidatePhysicalSize(IdentityCardLayout layout)
	{
		var (width, height) = PhysicalSize(layout);
		if (!InRange(width, 30, 200) || !InRange(height, 30, 200)) throw Invalid("Card dimensions must be 30 to 200 mm.");
		return (width, height);
	}
	public static BusinessException Invalid(string message) => new BusinessException(InvalidConfiguration).WithData("detail", message);
}
