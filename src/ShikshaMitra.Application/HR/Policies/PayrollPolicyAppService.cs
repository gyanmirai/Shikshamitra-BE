using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
[Authorize]
public class PayrollPolicyAppService(
	IRepository<PayrollPolicy, Guid> repository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarRepository,
	IHrPolicySafetyAppService policySafetyAppService,
	HrPolicyImpactConfirmationService confirmationService,
	HrPolicyVersionAppService versionAppService,
	HrAuditWriter auditWriter)
	: ApplicationService, IPayrollPolicyAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<PagedResultDto<PayrollPolicyDto>> GetListAsync(PolicyListRequestDto input)
	{
		var query = await repository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == input.AcademicYearId);
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			query = query.Where(x => x.Name.ToLower().Contains(search));
		}

		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query
			.OrderByDescending(x => x.IsDefault)
			.ThenBy(x => x.Name)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount));

		return new PagedResultDto<PayrollPolicyDto>(total, rows.Select(HrPolicyMapping.ToDto).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<PayrollPolicyDto> GetAsync(Guid id) =>
		HrPolicyMapping.ToDto(await repository.GetAsync(id));

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Create)]
	public async Task<PayrollPolicyDto> CreateAsync(CreateUpdatePayrollPolicyDto input)
	{
		await EnsureWorkCalendarAsync(input.WorkCalendarPolicyId, input.AcademicYearId);
		var status = HrPolicyMapping.ParsePolicyStatus(input.Status);
		var entity = new PayrollPolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			input.AcademicYearId,
			input.WorkCalendarPolicyId,
			input.Name,
			status,
			input.IsDefault,
			HrPolicyMapping.ParseDayCalculationType(input.DayCalculationType),
			input.ManualDays,
			HrPolicyMapping.ToManualMonths(input.ManualMonthDays),
			HrPolicyMapping.ParseSaturdayWorkPolicy(input.SaturdayWorkPolicy),
			HrPolicyMapping.ToHolidayOverrides(input.HolidayOverrides));

		if (entity.IsDefault)
		{
			await ClearDefaultsAsync(entity.AcademicYearId);
		}

		await repository.InsertAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("payroll", dto, "created");
		await auditWriter.WriteAsync(dto.AcademicYearId, "payroll_policy", dto.Id, dto.Name, "created", $"Created payroll policy {dto.Name}");
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Edit)]
	public async Task<PayrollPolicyDto> UpdateAsync(Guid id, CreateUpdatePayrollPolicyDto input)
	{
		await EnsureWorkCalendarAsync(input.WorkCalendarPolicyId, input.AcademicYearId);
		var entity = await repository.GetAsync(id);
		var before = HrPolicyMapping.ToDto(entity);
		var status = HrPolicyMapping.ParsePolicyStatus(input.Status);
		var dayCalculationType = HrPolicyMapping.ParseDayCalculationType(input.DayCalculationType);
		var manualMonthDays = HrPolicyMapping.ToManualMonths(input.ManualMonthDays);
		var saturdayWorkPolicy = HrPolicyMapping.ParseSaturdayWorkPolicy(input.SaturdayWorkPolicy);
		var holidayOverrides = HrPolicyMapping.ToHolidayOverrides(input.HolidayOverrides);

		if (IsUnchanged(entity, input, status, dayCalculationType, manualMonthDays, saturdayWorkPolicy, holidayOverrides))
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
			await ClearDefaultsAsync(entity.AcademicYearId, entity.Id);
		}

		await repository.UpdateAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("payroll", dto, "edited");
		await auditWriter.WriteAsync(dto.AcademicYearId, "payroll_policy", dto.Id, dto.Name, "edited", $"Edited payroll policy {dto.Name}", changedFields: HrAuditWriter.Diff(before, dto, "lastModificationTime", "lastModifierId"));
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Clone)]
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

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Archive)]
	public async Task DeleteAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
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

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Archive)]
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

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.SetDefault)]
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
		await ClearDefaultsAsync(entity.AcademicYearId, entity.Id);
		entity.SetDefault(true);
		await repository.UpdateAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("payroll", dto, "set-default");
		await auditWriter.WriteAsync(dto.AcademicYearId, "payroll_policy", dto.Id, dto.Name, "set_default", $"Set payroll policy {dto.Name} as default");
		return dto;
	}

	private async Task EnsureWorkCalendarAsync(Guid id, Guid academicYearId)
	{
		var workCalendar = await workCalendarRepository.GetAsync(id);
		if (workCalendar.AcademicYearId != academicYearId || workCalendar.Status == HrPolicyStatus.Archived)
		{
			throw new BusinessException("ShikshaMitra:InvalidWorkCalendarPolicy");
		}
	}

	private async Task ClearDefaultsAsync(Guid academicYearId, Guid? excludeId = null)
	{
		var query = await repository.GetQueryableAsync();
		var defaults = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.IsDefault &&
			(!excludeId.HasValue || x.Id != excludeId.Value)));

		foreach (var item in defaults)
		{
			item.SetDefault(false);
			await repository.UpdateAsync(item, autoSave: false);
			var dto = HrPolicyMapping.ToDto(item);
			await versionAppService.CaptureAsync("payroll", dto, "set-default-cleared");
			await auditWriter.WriteAsync(dto.AcademicYearId, "payroll_policy", dto.Id, dto.Name, "set_default", $"Cleared default payroll policy {dto.Name}");
		}
	}

	private static bool IsUnchanged(
		PayrollPolicy entity,
		CreateUpdatePayrollPolicyDto input,
		HrPolicyStatus status,
		DayCalculationType dayCalculationType,
		IReadOnlyList<DayCalculationManualMonth> manualMonthDays,
		SaturdayWorkPolicy saturdayWorkPolicy,
		IReadOnlyList<PayrollHolidayOverride> holidayOverrides)
	{
		return entity.AcademicYearId == input.AcademicYearId &&
			entity.WorkCalendarPolicyId == input.WorkCalendarPolicyId &&
			string.Equals(entity.Name, input.Name.Trim(), StringComparison.Ordinal) &&
			entity.Status == status &&
			entity.IsDefault == input.IsDefault &&
			entity.DayCalculationType == dayCalculationType &&
			entity.ManualDays == input.ManualDays &&
			entity.SaturdayWorkPolicy == saturdayWorkPolicy &&
			ManualMonthsEqual(entity.GetManualMonthDays(), manualMonthDays) &&
			HolidayOverridesEqual(entity.GetHolidayOverrides(), holidayOverrides);
	}

	private static bool ManualMonthsEqual(
		IReadOnlyList<DayCalculationManualMonth> left,
		IReadOnlyList<DayCalculationManualMonth> right) =>
		left.Count == right.Count && left.Zip(right).All(pair =>
			pair.First.Year == pair.Second.Year &&
			pair.First.Month == pair.Second.Month &&
			pair.First.ManualDays == pair.Second.ManualDays);

	private static bool HolidayOverridesEqual(
		IReadOnlyList<PayrollHolidayOverride> left,
		IReadOnlyList<PayrollHolidayOverride> right) =>
		left.Count == right.Count && left.Zip(right).All(pair =>
			pair.First.Date.Date == pair.Second.Date.Date &&
			string.Equals(pair.First.HolidayName, pair.Second.HolidayName, StringComparison.Ordinal) &&
			pair.First.IncludedInPayroll == pair.Second.IncludedInPayroll &&
			pair.First.CalendarHolidayId == pair.Second.CalendarHolidayId);
}
