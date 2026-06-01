using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShikshaMitra.HR;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.ICards;

internal sealed class EmployeeICardLayout
{
	public string Orientation { get; set; } = "Horizontal";
	public int Width { get; set; }
	public int Height { get; set; }
	public string BackgroundColor { get; set; } = "#FFFFFF";
	public string ThemeColor { get; set; } = "#1D4ED8";
	public EmployeeICardLayoutSide Front { get; set; } = new();
	public EmployeeICardLayoutSide? Back { get; set; }
}

internal sealed class EmployeeICardLayoutSide
{
	public List<EmployeeICardLayoutElement> Elements { get; set; } = new();
}

internal sealed class EmployeeICardLayoutElement
{
	public string Id { get; set; } = string.Empty;
	public string Type { get; set; } = string.Empty;
	public string? FieldKey { get; set; }
	public string? Label { get; set; }
	public string? Value { get; set; }
	public float X { get; set; }
	public float Y { get; set; }
	public float W { get; set; }
	public float H { get; set; }
	public int Z { get; set; }
	public bool Visible { get; set; } = true;
	public bool Locked { get; set; }
	public EmployeeICardElementStyle Style { get; set; } = new();
}

internal sealed class EmployeeICardElementStyle
{
	public string? Color { get; set; }
	public string? BackgroundColor { get; set; }
	public string? BorderColor { get; set; }
	public float? BorderWidth { get; set; }
	public float? BorderRadius { get; set; }
	public float? FontSize { get; set; }
	public object? FontWeight { get; set; }
	public string? TextAlign { get; set; }
	public bool? ShowLabel { get; set; }
	public string? Separator { get; set; }
	public string? PhotoShape { get; set; }
}

internal sealed class EmployeeICardPrintSettings
{
	public string PageSize { get; set; } = "A4";
	public int CardsPerPage { get; set; } = 8;
	public int Margin { get; set; } = 18;
	public int Gap { get; set; } = 10;
	public bool ShowCutMarks { get; set; } = true;
}

internal static class EmployeeICardLayoutSchema
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	private static readonly HashSet<string> SupportedElementTypes = new(StringComparer.OrdinalIgnoreCase)
	{
		"text",
		"photo",
		"logo",
		"qr",
		"barcode",
		"shape",
		"line"
	};

	private static readonly HashSet<string> SupportedFieldKeys = new(StringComparer.OrdinalIgnoreCase)
	{
		"photo",
		"name",
		"employeeCode",
		"designation",
		"department",
		"organization",
		"contactNo",
		"bloodGroup",
		"validityYear",
		"address",
		"schoolName",
		"schoolAddress",
		"static"
	};

	public static EmployeeICardLayout ResolveLayout(EmployeeICardTemplate template)
	{
		if (!string.IsNullOrWhiteSpace(template.LayoutJson))
		{
			var layout = ParseLayout(template.LayoutJson);
			if (layout is not null)
			{
				return layout;
			}
		}

		return BuildLegacyLayout(template);
	}

	public static EmployeeICardPrintSettings ResolvePrintSettings(string? json, int? cardsPerPageFallback)
	{
		var fallback = Math.Clamp(cardsPerPageFallback ?? 8, 1, 10);
		if (string.IsNullOrWhiteSpace(json))
		{
			return new EmployeeICardPrintSettings { CardsPerPage = fallback };
		}

		try
		{
			var settings = JsonSerializer.Deserialize<EmployeeICardPrintSettings>(json, JsonOptions) ?? new EmployeeICardPrintSettings();
			settings.PageSize = "A4";
			settings.CardsPerPage = Math.Clamp(settings.CardsPerPage <= 0 ? fallback : settings.CardsPerPage, 1, 10);
			settings.Margin = Math.Clamp(settings.Margin, 0, 60);
			settings.Gap = Math.Clamp(settings.Gap, 0, 40);
			return settings;
		}
		catch (JsonException)
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson);
		}
	}

	public static void Validate(string? layoutJson, string? printSettingsJson)
	{
		if (!string.IsNullOrWhiteSpace(layoutJson))
		{
			_ = ParseLayout(layoutJson) ?? throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson);
		}

		_ = ResolvePrintSettings(printSettingsJson, 8);
	}

	private static EmployeeICardLayout? ParseLayout(string json)
	{
		try
		{
			var layout = JsonSerializer.Deserialize<EmployeeICardLayout>(json, JsonOptions);
			if (layout is null ||
				layout.Front?.Elements is null ||
				layout.Front.Elements.Count == 0 ||
				layout.Back?.Elements is null && layout.Back is not null ||
				layout.Width is < 100 or > 2000 ||
				layout.Height is < 100 or > 2000 ||
				!IsSupportedOrientation(layout.Orientation))
			{
				throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson);
			}

			ValidateColor(layout.BackgroundColor);
			ValidateColor(layout.ThemeColor);

			foreach (var element in layout.Front.Elements.Concat(layout.Back?.Elements ?? Enumerable.Empty<EmployeeICardLayoutElement>()))
			{
				ValidateElement(element, layout.Width, layout.Height);
			}

			return layout;
		}
		catch (JsonException)
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson);
		}
	}

	private static void ValidateElement(EmployeeICardLayoutElement element, int cardWidth, int cardHeight)
	{
		if (element is null ||
			element.Style is null ||
			string.IsNullOrWhiteSpace(element.Id) ||
			!SupportedElementTypes.Contains(element.Type) ||
			element.W <= 0 ||
			element.H <= 0 ||
			element.X < -cardWidth ||
			element.Y < -cardHeight ||
			element.X > cardWidth * 2 ||
			element.Y > cardHeight * 2)
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson);
		}

		if (!string.IsNullOrWhiteSpace(element.FieldKey) && !SupportedFieldKeys.Contains(element.FieldKey))
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardFieldSchemaUnsupported);
		}

		ValidateOptionalColor(element.Style.Color);
		ValidateOptionalColor(element.Style.BackgroundColor);
		ValidateOptionalColor(element.Style.BorderColor);
	}

	private static EmployeeICardLayout BuildLegacyLayout(EmployeeICardTemplate template)
	{
		var isVertical = string.Equals(template.Orientation, "Vertical", StringComparison.OrdinalIgnoreCase);
		var width = template.Width > 0 ? template.Width : isVertical ? 280 : 420;
		var height = template.Height > 0 ? template.Height : isVertical ? 420 : 260;
		var theme = NormalizeColor(template.ThemeColor ?? "#1D4ED8");
		var background = NormalizeColor(template.BackgroundColor ?? "#F8FAFC");
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
				: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "photo", "name", "employeeCode" };
		}
		catch
		{
			return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "photo", "name", "employeeCode" };
		}
	}

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

	private static bool IsSupportedOrientation(string value) =>
		string.Equals(value, "Horizontal", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(value, "Vertical", StringComparison.OrdinalIgnoreCase);

	private static void ValidateOptionalColor(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			ValidateColor(value);
		}
	}

	private static void ValidateColor(string value)
	{
		var color = NormalizeColor(value);
		var hex = color[1..];
		if (hex.Length is not 6 || hex.Any(character => !Uri.IsHexDigit(character)))
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson);
		}
	}

	private static string NormalizeColor(string value) =>
		value.StartsWith("#", StringComparison.Ordinal) ? value : $"#{value}";
}
