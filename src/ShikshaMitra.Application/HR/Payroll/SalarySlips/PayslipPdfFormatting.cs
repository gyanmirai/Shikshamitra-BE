using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

internal static class PayslipPdfFormatting
{
	private static readonly CultureInfo IndianCulture = CultureInfo.GetCultureInfo("en-IN");

	public static IContainer HeaderCell(IContainer container, string background) =>
		container.Background(background).Border(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).PaddingHorizontal(6);

	public static IContainer BodyCell(IContainer container) =>
		container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5).PaddingHorizontal(6);

	public static IContainer TotalCell(IContainer container, string background) =>
		container.Background(background).BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(6).PaddingHorizontal(6);

	public static string FormatCurrency(decimal amount) =>
		$"Rs. {amount.ToString("N2", IndianCulture)}";

	public static string FormatDays(decimal? days) =>
		days.HasValue ? days.Value.ToString("0.##", CultureInfo.InvariantCulture) : "-";
}
