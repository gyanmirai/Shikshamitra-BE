using System;

namespace ShikshaMitra.HR.Employees.ICards;

internal static class EmployeeICardPdfLayoutResolver
{
	private const decimal PreviewPixelToPointScale = 0.7m;
	private const string VerticalOrientation = "Vertical";

	public static (float Width, float Height) ResolveCardSize(EmployeeICardLayout layout)
	{
		var isVertical = string.Equals(layout.Orientation, VerticalOrientation, StringComparison.OrdinalIgnoreCase);
		var defaultWidth = isVertical ? 160f : 252f;
		var defaultHeight = isVertical ? 252f : 160f;
		if (layout.Width <= 0 || layout.Height <= 0)
		{
			return (defaultWidth, defaultHeight);
		}

		var width = (float) (layout.Width * PreviewPixelToPointScale);
		var height = (float) (layout.Height * PreviewPixelToPointScale);
		if ((isVertical && width > height) || (!isVertical && height > width))
		{
			(width, height) = (height, width);
		}

		return (Math.Clamp(width, 120f, 280f), Math.Clamp(height, 120f, 390f));
	}

	public static (int Columns, float CardWidth, float CardHeight) ResolveSheetLayout(
		int cardsPerPage,
		float cardWidth,
		float cardHeight,
		EmployeeICardPrintSettings printSettings)
	{
		const float a4Width = 595f;
		const float a4Height = 842f;
		var margin = printSettings.Margin;
		var spacing = printSettings.Gap;
		var availableWidth = a4Width - (margin * 2);
		var availableHeight = a4Height - (margin * 2);
		var columns = Math.Clamp((int) Math.Floor((availableWidth + spacing) / (cardWidth + spacing)), 1, cardsPerPage);
		var rows = (int) Math.Ceiling(cardsPerPage / (double) columns);
		var totalWidth = (columns * cardWidth) + ((columns - 1) * spacing);
		var totalHeight = (rows * cardHeight) + ((rows - 1) * spacing);
		var scale = Math.Min(1f, Math.Min(availableWidth / totalWidth, availableHeight / totalHeight));

		return (columns, cardWidth * scale, cardHeight * scale);
	}
}
