using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp;
using Volo.Abp.Data;
using ShikshaMitra.TenantSettings.SchoolIdentity;

namespace ShikshaMitra.HR.Employees.ICards;

[RemoteService(false)]
public class EmployeeICardTemplateMutationService(
	IRepository<EmployeeICardTemplate, Guid> repository,
	IGuidGenerator guidGenerator) : ApplicationService, ITransientDependency
{
	public async Task<EmployeeICardTemplateDto> CreateAsync(EmployeeICardTemplateRequest input)
	{
		EmployeeICardTemplateRules.ValidateFieldsJson(input.FieldsJson);
		EmployeeICardTemplateRules.ValidateTemplateInput(input, CurrentTenant.Id);

		var template = new EmployeeICardTemplate(
			guidGenerator.Create(),
			CurrentTenant.Id,
			input.AcademicYearId,
			input.MediumStageId,
			input.Name,
			input.Width,
			input.Height,
			input.BackgroundColor,
			input.BackgroundImageBlobName,
			input.LogoBlobName,
			input.SchoolName,
			input.SchoolAddress,
			input.ThemeColor,
			input.Orientation,
			input.PresetKey,
			input.QrCodeEnabled,
			input.BarcodeEnabled,
			input.BackSideEnabled,
			input.SchemaVersion,
			input.LayoutJson,
			input.PrintSettingsJson,
			input.FieldsJson,
			input.IsActive);

		SetLogoMode(template, input);
		await repository.InsertAsync(template, autoSave: true);
		return EmployeeICardTemplateProjection.ToDto(template);
	}

	public async Task<EmployeeICardTemplateDto> UpdateAsync(Guid id, EmployeeICardTemplateRequest input)
	{
		EmployeeICardTemplateRules.ValidateFieldsJson(input.FieldsJson);
		EmployeeICardTemplateRules.ValidateTemplateInput(input, CurrentTenant.Id);

		var template = await repository.GetAsync(id);
		template.Update(
			input.AcademicYearId,
			input.MediumStageId,
			input.Name,
			input.Width,
			input.Height,
			input.BackgroundColor,
			input.BackgroundImageBlobName,
			input.LogoBlobName,
			input.SchoolName,
			input.SchoolAddress,
			input.ThemeColor,
			input.Orientation,
			input.PresetKey,
			input.QrCodeEnabled,
			input.BarcodeEnabled,
			input.BackSideEnabled,
			input.SchemaVersion,
			input.LayoutJson,
			input.PrintSettingsJson,
			input.FieldsJson,
			input.IsActive);

		SetLogoMode(template, input);
		await repository.UpdateAsync(template, autoSave: true);
		return EmployeeICardTemplateProjection.ToDto(template);
	}

	public Task DeleteAsync(Guid id) =>
		repository.DeleteAsync(id, autoSave: true);

	private static void SetLogoMode(EmployeeICardTemplate template, EmployeeICardTemplateRequest input)
	{
		if (input.LogoMode == null) return;
		if (!Enum.IsDefined(input.LogoMode.Value) ||
			(input.LogoMode == SchoolIdentityAssetMode.Custom && string.IsNullOrWhiteSpace(input.LogoBlobName)))
			throw new BusinessException(SchoolIdentityErrorCodes.InvalidSelection);
		template.SetProperty("SchoolIdentityLogoMode", (int)input.LogoMode.Value);
	}
}
