using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.HR.Employees.ICards;

internal static class EmployeeICardLegacyLayoutBuilder
{
	public static EmployeeICardLayout Build(EmployeeICardTemplate template)
	{
		var isVertical = string.Equals(template.Orientation, "Vertical", StringComparison.OrdinalIgnoreCase);
		var width = template.Width > 0 ? template.Width : isVertical ? 280 : 420;
		var height = template.Height > 0 ? template.Height : isVertical ? 420 : 260;
		var theme = EmployeeICardLayoutValidator.NormalizeColor(template.ThemeColor ?? "#1D4ED8");
		var background = EmployeeICardLayoutValidator.NormalizeColor(template.BackgroundColor ?? "#F8FAFC");
		var fields = BuildLegacyFieldSet(template.FieldsJson);
		var elements = new List<EmployeeICardLayoutElement>
		{
			Box("accent", 0, 0, width, 70, 0, background),
			Logo("logo", 18, 14, 44, 44, 2),
			Text("school", "schoolName", 72, 14, width - 92, 22, 3, theme, 15, true),
			Text("address", "schoolAddress", 72, 38, width - 92, 20, 3, "#64748B", 8, false),
			Photo("photo", 22, 88, isVertical ? 108 : 78, isVertical ? 120 : 92, 4, theme),
			Text("name", "name", isVertical ? 28 : 116, isVertical ? 224 : 88, isVertical ? width - 56 : 190, 28, 5, "#0F172A", 18, true),
			Text("designation", "designation", isVertical ? 28 : 116, isVertical ? 258 : 118, 170, 20, 5, "#475569", 11, false),
			Text("code", "employeeCode", isVertical ? 28 : 116, isVertical ? 290 : 146, 160, 18, 5, "#111827", 10, true, "Code"),
			Text("department", "department", isVertical ? 28 : 116, isVertical ? 314 : 170, 160, 18, 5, "#111827", 9, false, "Dept"),
			Text("phone", "contactNo", isVertical ? 28 : 116, isVertical ? 338 : 194, 160, 18, 5, "#111827", 9, false, "Phone"),
			Qr("qr", width - 76, height - 74, 54, 54, 6, template.QrCodeEnabled),
			Barcode("barcode", 22, height - 38, 150, 22, 6, template.BarcodeEnabled)
		};

		foreach (var element in elements)
		{
			if (!string.IsNullOrWhiteSpace(element.FieldKey) &&
				!element.FieldKey.StartsWith("school", StringComparison.OrdinalIgnoreCase) &&
				!string.Equals(element.FieldKey, "photo", StringComparison.OrdinalIgnoreCase))
			{
				element.Visible = fields.Contains(element.FieldKey);
			}
		}

		return new EmployeeICardLayout
		{
			Orientation = isVertical ? "Vertical" : "Horizontal",
			Width = width,
			Height = height,
			BackgroundColor = background,
			ThemeColor = theme,
			Front = new EmployeeICardLayoutSide { Elements = elements }
		};
	}

	private static HashSet<string> BuildLegacyFieldSet(string fieldsJson)
	{
		try
		{
			using var document = JsonDocument.Parse(fieldsJson);
			return document.RootElement.ValueKind == JsonValueKind.Array
				? document.RootElement.EnumerateArray()
					.Where(x => x.TryGetProperty("name", out _) &&
						(!x.TryGetProperty("enabled", out var enabled) || enabled.ValueKind != JsonValueKind.False))
					.Select(x => x.GetProperty("name").GetString() ?? string.Empty)
					.Where(x => !string.IsNullOrWhiteSpace(x))
					.ToHashSet(StringComparer.OrdinalIgnoreCase)
				: DefaultLegacyFields();
		}
		catch
		{
			return DefaultLegacyFields();
		}
	}

	private static HashSet<string> DefaultLegacyFields() =>
		new(StringComparer.OrdinalIgnoreCase) { "photo", "name", "employeeCode" };

	private static EmployeeICardLayoutElement Text(string id, string fieldKey, float x, float y, float w, float h, int z, string color, float fontSize, bool bold, string? label = null) =>
		new() { Id = id, Type = "text", FieldKey = fieldKey, X = x, Y = y, W = w, H = h, Z = z, Label = label, Style = new EmployeeICardElementStyle { Color = color, FontSize = fontSize, FontWeight = bold ? "700" : "400", ShowLabel = !string.IsNullOrWhiteSpace(label), Separator = ": " } };

	private static EmployeeICardLayoutElement Photo(string id, float x, float y, float w, float h, int z, string themeColor) =>
		new() { Id = id, Type = "photo", FieldKey = "photo", X = x, Y = y, W = w, H = h, Z = z, Style = new EmployeeICardElementStyle { BorderColor = themeColor, BorderWidth = 1, PhotoShape = "rounded" } };

	private static EmployeeICardLayoutElement Logo(string id, float x, float y, float w, float h, int z) =>
		new() { Id = id, Type = "logo", X = x, Y = y, W = w, H = h, Z = z };

	private static EmployeeICardLayoutElement Qr(string id, float x, float y, float w, float h, int z, bool visible = true) =>
		new() { Id = id, Type = "qr", X = x, Y = y, W = w, H = h, Z = z, Visible = visible };

	private static EmployeeICardLayoutElement Barcode(string id, float x, float y, float w, float h, int z, bool visible = true) =>
		new() { Id = id, Type = "barcode", X = x, Y = y, W = w, H = h, Z = z, Visible = visible };

	private static EmployeeICardLayoutElement Box(string id, float x, float y, float w, float h, int z, string backgroundColor) =>
		new() { Id = id, Type = "shape", X = x, Y = y, W = w, H = h, Z = z, Style = new EmployeeICardElementStyle { BackgroundColor = backgroundColor } };
}
