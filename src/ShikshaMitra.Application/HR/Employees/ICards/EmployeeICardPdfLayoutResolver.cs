using ShikshaMitra.Common.IdentityCards;

namespace ShikshaMitra.HR.Employees.ICards;

internal static class EmployeeICardPdfLayoutResolver
{
	public static (float Width, float Height) ResolveCardSize(IdentityCardLayout layout)
	{
		var size = IdentityCardGeometry.PhysicalSize(layout);
		return (size.Width * IdentityCardGeometry.PointsPerMm, size.Height * IdentityCardGeometry.PointsPerMm);
	}

	public static (int Columns, float CardWidth, float CardHeight) ResolveSheetLayout(int cardsPerPage, float cardWidth, float cardHeight, IdentityCardPrintSettings settings)
	{
		var sheet = IdentityCardGeometry.Sheet(new IdentityCardLayout { WidthMm = cardWidth / IdentityCardGeometry.PointsPerMm, HeightMm = cardHeight / IdentityCardGeometry.PointsPerMm }, settings);
		return (sheet.Columns, cardWidth, cardHeight);
	}
}
