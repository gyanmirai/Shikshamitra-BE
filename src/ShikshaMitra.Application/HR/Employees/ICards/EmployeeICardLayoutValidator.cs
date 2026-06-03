using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.HR;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.ICards;

internal static class EmployeeICardLayoutValidator
{
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

	public static EmployeeICardLayout? ParseLayout(string json)
	{
		try
		{
			var layout = JsonSerializer.Deserialize<EmployeeICardLayout>(json, EmployeeICardLayoutJson.Options);
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

	public static string NormalizeColor(string value) =>
		value.StartsWith("#", StringComparison.Ordinal) ? value : $"#{value}";

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
}
