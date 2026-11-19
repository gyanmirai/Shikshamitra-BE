using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Globalization;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

internal static class PayslipPdfFormatting
{
	private static readonly CultureInfo IndianCulture = CultureInfo.GetCultureInfo("en-IN");
	private const string Border = "#E5E7EB";
	private const string HeaderBackground = "#F9FAFB";

	// Keep the recorded logical text within one page's corresponding glyph content.
	// A spanning replacement repeats in PDF.js's page-specific structure trees.
	public static TextBlockDescriptor LogicalText(this IContainer container, string value) =>
		container.ShowEntire().SemanticParagraph().SemanticSpan(value).Text(value);

	public static IContainer HeaderCell(IContainer container) =>
		container.Background(HeaderBackground).BorderTop(1).BorderBottom(1).BorderColor(Border).PaddingVertical(5).PaddingHorizontal(6);

	public static IContainer BodyCell(IContainer container) =>
		container.BorderBottom(1).BorderColor(Border).PaddingVertical(5).PaddingHorizontal(6);

	public static IContainer TotalCell(IContainer container) =>
		container.Background(HeaderBackground).BorderTop(1).BorderColor(Border).PaddingVertical(6).PaddingHorizontal(6);

	public static string FormatCurrency(decimal amount) =>
		$"Rs. {amount.ToString(amount == decimal.Truncate(amount) ? "N0" : "N2", IndianCulture)}";

	public static string FormatDays(decimal? days) =>
		days.HasValue ? days.Value.ToString("0.##", CultureInfo.InvariantCulture) : "-";

	public static string FormatEmployeeComponentName(string name)
	{
		const string insuranceName = "Employee State Insurance (ESI) - Auto";
		if (name == insuranceName) return "Employee ESI";
		const string correctionPrefix = insuranceName + " (";
		if (name.StartsWith(correctionPrefix, StringComparison.Ordinal) &&
			DateTime.TryParseExact(name[correctionPrefix.Length..], "yyyy-MM' correction)'",
				CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
			return "Employee ESI" + name[insuranceName.Length..];
		return name;
	}
}
