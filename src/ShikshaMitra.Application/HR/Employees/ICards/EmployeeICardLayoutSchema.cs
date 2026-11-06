using ShikshaMitra.Common.IdentityCards;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.ICards;

internal static class EmployeeICardLayoutSchema
{
	public static IdentityCardLayout ResolveLayout(EmployeeICardTemplate template) => EmployeeICardLayoutValidator.ParseLayout(template.LayoutJson, template.SchemaVersion, template.BackSideEnabled);

	public static IdentityCardPrintSettings ResolvePrintSettings(string? json, int? cardsPerPageFallback, int schemaVersion = 1)
	{
		try { return IdentityCardSchema.ParsePrint(json, cardsPerPageFallback, schemaVersion); }
		catch (BusinessException ex) when (ex.Code == IdentityCardGeometry.InvalidConfiguration)
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson).WithData("detail", ex.Data["detail"] ?? "Invalid identity card configuration.");
		}
	}

	public static void Validate(string? layoutJson, string? printSettingsJson, int schemaVersion = 1, bool backEnabled = false)
	{
		if (schemaVersion is not (1 or 2)) throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson);
		var layout = EmployeeICardLayoutValidator.ParseLayout(layoutJson ?? "", schemaVersion, backEnabled);
		var settings = ResolvePrintSettings(printSettingsJson, null, schemaVersion);
		_ = IdentityCardGeometry.Sheet(layout, settings);
		if (settings.SideMode == "back" && !backEnabled) throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson);
	}
}
