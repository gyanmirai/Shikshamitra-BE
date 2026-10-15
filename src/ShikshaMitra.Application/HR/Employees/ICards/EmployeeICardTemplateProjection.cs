using Volo.Abp.Data;
using ShikshaMitra.TenantSettings.SchoolIdentity;

namespace ShikshaMitra.HR.Employees.ICards;

internal static class EmployeeICardTemplateProjection
{
	public static EmployeeICardTemplateDto ToDto(EmployeeICardTemplate template)
	{
		return new EmployeeICardTemplateDto
		{
			Id = template.Id,
			LogoMode = (SchoolIdentityAssetMode?)template.GetProperty<int?>("SchoolIdentityLogoMode"),
			AcademicYearId = template.AcademicYearId,
			MediumStageId = template.MediumStageId,
			Name = template.Name,
			Width = template.Width,
			Height = template.Height,
			BackgroundColor = template.BackgroundColor,
			BackgroundImageBlobName = template.BackgroundImageBlobName,
			LogoBlobName = template.LogoBlobName,
			SchoolName = template.SchoolName,
			SchoolAddress = template.SchoolAddress,
			ThemeColor = template.ThemeColor,
			Orientation = template.Orientation,
			PresetKey = template.PresetKey,
			QrCodeEnabled = template.QrCodeEnabled,
			BarcodeEnabled = template.BarcodeEnabled,
			BackSideEnabled = template.BackSideEnabled,
			SchemaVersion = template.SchemaVersion,
			LayoutJson = template.LayoutJson,
			PrintSettingsJson = template.PrintSettingsJson,
			FieldsJson = template.FieldsJson,
			IsActive = template.IsActive,
			CreationTime = template.CreationTime,
			LastModificationTime = template.LastModificationTime
		};
	}
}
