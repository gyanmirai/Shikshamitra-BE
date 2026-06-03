using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class PayrollPolicyWorkflow(
	IRepository<PayrollPolicy, Guid> repository,
	IHrPolicySafetyAppService policySafetyAppService,
	HrPolicyImpactConfirmationService confirmationService,
	HrPolicyVersionAppService versionAppService,
	PayrollPolicyDefaultService defaultService,
	HrPolicyWorkCalendarValidator workCalendarValidator,
	HrAuditWriter auditWriter) : ApplicationService, ITransientDependency
{
	public async Task<PayrollPolicyDto> CreateAsync(CreateUpdatePayrollPolicyDto input)
	{
		await workCalendarValidator.EnsureUsableAsync(input.WorkCalendarPolicyId, input.AcademicYearId);
		var entity = PayrollPolicyModelBuilder.Build(GuidGenerator.Create(), CurrentTenant.Id, input);

		if (entity.IsDefault)
		{
			await defaultService.ClearDefaultsAsync(entity.AcademicYearId);
		}

		await repository.InsertAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("payroll", dto, "created");
		await auditWriter.WriteAsync(dto.AcademicYearId, "payroll_policy", dto.Id, dto.Name, "created", $"Created payroll policy {dto.Name}");
		return dto;
	}

	public async Task<PayrollPolicyDto> UpdateAsync(Guid id, CreateUpdatePayrollPolicyDto input)
	{
		await workCalendarValidator.EnsureUsableAsync(input.WorkCalendarPolicyId, input.AcademicYearId);
		var entity = await repository.GetAsync(id);
		var before = HrPolicyMapping.ToDto(entity);
		var status = HrPolicyMapping.ParsePolicyStatus(input.Status);
		var dayCalculationType = HrPolicyMapping.ParseDayCalculationType(input.DayCalculationType);
		var manualMonthDays = HrPolicyMapping.ToManualMonths(input.ManualMonthDays);
		var saturdayWorkPolicy = HrPolicyMapping.ParseSaturdayWorkPolicy(input.SaturdayWorkPolicy);
		var holidayOverrides = HrPolicyMapping.ToHolidayOverrides(input.HolidayOverrides);

		if (PayrollPolicyModelBuilder.IsUnchanged(entity, input, status, dayCalculationType, manualMonthDays, saturdayWorkPolicy, holidayOverrides))
		{
			return before;
		}

		if (entity.Status != HrPolicyStatus.Draft || status != HrPolicyStatus.Draft)
		{
			var preview = await policySafetyAppService.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
			{
				PolicyType = "payroll",
				PolicyId = id,
				AcademicYearId = entity.AcademicYearId,
				Action = "update",
				PayrollPolicy = input
			});
			await confirmationService.EnsureConfirmedAsync(preview);
		}

		await defaultService.EnsureDefaultRemainsAsync(entity, input.IsDefault, status);
		entity.Update(
			input.WorkCalendarPolicyId,
			input.Name,
			status,
			input.IsDefault,
			dayCalculationType,
			input.ManualDays,
			manualMonthDays,
			saturdayWorkPolicy,
			holidayOverrides);

		if (entity.IsDefault)
		{
			await defaultService.ClearDefaultsAsync(entity.AcademicYearId, entity.Id);
		}

		await repository.UpdateAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("payroll", dto, "edited");
		await auditWriter.WriteAsync(dto.AcademicYearId, "payroll_policy", dto.Id, dto.Name, "edited", $"Edited payroll policy {dto.Name}", changedFields: HrAuditWriter.Diff(before, dto, "lastModificationTime", "lastModifierId"));
		return dto;
	}

	public async Task<PayrollPolicyDto> CloneAsync(Guid id, HrPolicyCloneRequestDto input)
	{
		var source = await repository.GetAsync(id);
		var entity = new PayrollPolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			source.AcademicYearId,
			source.WorkCalendarPolicyId,
			string.IsNullOrWhiteSpace(input.Name) ? $"{source.Name} Copy" : input.Name!,
			HrPolicyStatus.Draft,
			false,
			source.DayCalculationType,
			source.ManualDays,
			source.GetManualMonthDays(),
			source.SaturdayWorkPolicy,
			source.GetHolidayOverrides(),
			source.GetLockedMonthSettings());

		await repository.InsertAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("payroll", dto, "cloned");
		await auditWriter.WriteAsync(dto.AcademicYearId, "payroll_policy", dto.Id, dto.Name, "cloned", $"Cloned payroll policy from {source.Name}", context: new { sourcePolicyId = source.Id });
		return dto;
	}

	public async Task DeleteAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		await defaultService.EnsureDefaultRemainsAsync(entity, isDefaultAfterUpdate: false, statusAfterUpdate: HrPolicyStatus.Archived);
		var preview = await policySafetyAppService.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "payroll",
			PolicyId = id,
			AcademicYearId = entity.AcademicYearId,
			Action = "archive"
		});
		await confirmationService.EnsureConfirmedAsync(preview);
		entity.Archive();
		await repository.UpdateAsync(entity, autoSave: true);
		var archivedDto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("payroll", archivedDto, "archived");
		await auditWriter.WriteAsync(archivedDto.AcademicYearId, "payroll_policy", archivedDto.Id, archivedDto.Name, "archived", $"Archived payroll policy {archivedDto.Name}");
	}

	public async Task<PayrollPolicyDto> UnarchiveAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		if (entity.Status != HrPolicyStatus.Archived)
		{
			return HrPolicyMapping.ToDto(entity);
		}

		entity.Unarchive();
		await repository.UpdateAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("payroll", dto, "unarchived");
		await auditWriter.WriteAsync(dto.AcademicYearId, "payroll_policy", dto.Id, dto.Name, "unarchived", $"Unarchived payroll policy {dto.Name} as draft.");
		return dto;
	}

	public async Task<PayrollPolicyDto> SetDefaultAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		HrPolicyMapping.EnsureActive(entity.Status);
		var preview = await policySafetyAppService.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "payroll",
			PolicyId = id,
			AcademicYearId = entity.AcademicYearId,
			Action = "set-default"
		});
		await confirmationService.EnsureConfirmedAsync(preview);
		await defaultService.ClearDefaultsAsync(entity.AcademicYearId, entity.Id);
		entity.SetDefault(true);
		await repository.UpdateAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("payroll", dto, "set-default");
		await auditWriter.WriteAsync(dto.AcademicYearId, "payroll_policy", dto.Id, dto.Name, "set_default", $"Set payroll policy {dto.Name} as default");
		return dto;
	}

}
