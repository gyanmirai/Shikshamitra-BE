using System;

namespace ShikshaMitra.HR.Employees.ICards;

internal static class EmployeeICardTextRenderer
{
	public static string Render(
		EmployeeICardTemplate template,
		Employee employee,
		string? organization,
		EmployeeICardLayoutElement element)
	{
		var style = element.Style ?? new EmployeeICardElementStyle();
		var value = EmployeeICardPdfFieldResolver.ResolveFieldValue(template, employee, organization, element.FieldKey, element.Value);
		if (string.IsNullOrWhiteSpace(value))
		{
			value = "-";
		}

		if (style.ShowLabel == true && !string.IsNullOrWhiteSpace(element.Label))
		{
			value = $"{element.Label}{style.Separator ?? ": "}{value}";
		}

		var fontSize = Math.Clamp(style.FontSize ?? 9, 5, 48);
		var color = EmployeeICardSvgFormatting.NormalizeColor(style.Color ?? "#111827");
		var fontWeightValue = style.FontWeight?.ToString();
		var fontWeight = fontWeightValue is "700" or "800" or "900" or "bold" ? "700" : "400";
		var anchor = string.Equals(style.TextAlign, "center", StringComparison.OrdinalIgnoreCase)
			? "middle"
			: string.Equals(style.TextAlign, "right", StringComparison.OrdinalIgnoreCase) ? "end" : "start";
		var x = anchor == "middle" ? element.X + (element.W / 2) : anchor == "end" ? element.X + element.W : element.X;
		var y = element.Y + fontSize;
		var text = EmployeeICardSvgFormatting.Truncate(value, Math.Max(4, (int) (element.W / Math.Max(4, fontSize * 0.55f))));

		return $"""<text x="{x}" y="{y}" font-family="Arial, sans-serif" font-size="{fontSize}" font-weight="{fontWeight}" fill="{EmployeeICardSvgFormatting.Escape(color)}" text-anchor="{anchor}">{EmployeeICardSvgFormatting.Escape(text)}</text>""";
	}
}
