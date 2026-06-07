using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

internal static class PayslipPdfFormatting
{
	private static readonly CultureInfo IndianCulture = CultureInfo.GetCultureInfo("en-IN");
	private const string Border = "#E5E7EB";
	private const string HeaderBackground = "#F9FAFB";

	public static IContainer HeaderCell(IContainer container) =>
		container.Background(HeaderBackground).BorderTop(1).BorderBottom(1).BorderColor(Border).PaddingVertical(5).PaddingHorizontal(6);

	public static IContainer BodyCell(IContainer container) =>
		container.BorderBottom(1).BorderColor(Border).PaddingVertical(5).PaddingHorizontal(6);

	public static IContainer TotalCell(IContainer container) =>
		container.Background(HeaderBackground).BorderTop(1).BorderColor(Border).PaddingVertical(6).PaddingHorizontal(6);

	public static string FormatCurrency(decimal amount) =>
		$"Rs. {amount.ToString("N2", IndianCulture)}";

	public static string FormatDays(decimal? days) =>
		days.HasValue ? days.Value.ToString("0.##", CultureInfo.InvariantCulture) : "-";
}
