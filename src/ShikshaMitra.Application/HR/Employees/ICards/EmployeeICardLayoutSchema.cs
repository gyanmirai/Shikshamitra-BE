using System;
using System.Text.Json;
using ShikshaMitra.HR;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.ICards;

internal static class EmployeeICardLayoutSchema
{
	public static EmployeeICardLayout ResolveLayout(EmployeeICardTemplate template)
	{
		if (!string.IsNullOrWhiteSpace(template.LayoutJson))
		{
			var layout = EmployeeICardLayoutValidator.ParseLayout(template.LayoutJson);
			if (layout is not null)
			{
				return layout;
			}
		}

		return EmployeeICardLegacyLayoutBuilder.Build(template);
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
			var settings = JsonSerializer.Deserialize<EmployeeICardPrintSettings>(json, EmployeeICardLayoutJson.Options) ?? new EmployeeICardPrintSettings();
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
			_ = EmployeeICardLayoutValidator.ParseLayout(layoutJson) ?? throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson);
		}

		_ = ResolvePrintSettings(printSettingsJson, 8);
	}
}
