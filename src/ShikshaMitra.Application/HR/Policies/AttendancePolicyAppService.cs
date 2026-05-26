using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Audit;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
[Authorize]
public class AttendancePolicyAppService(
	IRepository<AttendancePolicy, Guid> repository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarRepository,
	IHrPolicySafetyAppService policySafetyAppService,
	HrPolicyImpactConfirmationService confirmationService,
	HrPolicyVersionAppService versionAppService,
	HrAuditWriter auditWriter)
	: ApplicationService, IAttendancePolicyAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<PagedResultDto<AttendancePolicyDto>> GetListAsync(PolicyListRequestDto input)
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

		return new PagedResultDto<AttendancePolicyDto>(total, rows.Select(HrPolicyMapping.ToDto).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<AttendancePolicyDto> GetAsync(Guid id) =>
		HrPolicyMapping.ToDto(await repository.GetAsync(id));

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Create)]
	public async Task<AttendancePolicyDto> CreateAsync(CreateUpdateAttendancePolicyDto input)
	{
		await EnsureWorkCalendarAsync(input.WorkCalendarPolicyId, input.AcademicYearId);
		var entity = BuildEntity(GuidGenerator.Create(), input);
		if (entity.IsDefault)
		{
			await ClearDefaultsAsync(entity.AcademicYearId);
		}

		await repository.InsertAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("attendance", dto, "created");
		await auditWriter.WriteAsync(entity.AcademicYearId, "attendance_policy", entity.Id, entity.Name, "created", $"Created attendance policy {entity.Name}.");
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Edit)]
	public async Task<AttendancePolicyDto> UpdateAsync(Guid id, CreateUpdateAttendancePolicyDto input)
	{
		await EnsureWorkCalendarAsync(input.WorkCalendarPolicyId, input.AcademicYearId);
		var entity = await repository.GetAsync(id);
		var status = HrPolicyMapping.ParsePolicyStatus(input.Status);
		var missingPunchBehavior = HrPolicyMapping.ParseMissingPunchBehavior(input.MissingPunchBehavior);
		var penaltyType = HrPolicyMapping.ParsePenaltyType(input.PenaltyType);
		var overtimeRate = HrPolicyMapping.ParseOvertimeRate(input.OvertimeRate);
		var holidayWorkBehavior = HrPolicyMapping.ParseHolidayWorkBehavior(input.HolidayWorkBehavior);
		var before = HrPolicyMapping.ToDto(entity);

		if (IsUnchanged(
			entity,
			input,
			status,
			missingPunchBehavior,
			penaltyType,
			overtimeRate,
			holidayWorkBehavior))
		{
			return before;
		}

		if (entity.Status != HrPolicyStatus.Draft || status != HrPolicyStatus.Draft)
		{
			var preview = await policySafetyAppService.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
			{
				PolicyType = "attendance",
				PolicyId = id,
				AcademicYearId = entity.AcademicYearId,
				Action = "update",
				AttendancePolicy = input
			});
			await confirmationService.EnsureConfirmedAsync(preview);
		}
		entity.Update(
			input.WorkCalendarPolicyId,
			input.Name,
			status,
			input.IsDefault,
			input.FullDayThresholdPercentage,
			input.HalfDayThresholdPercentage,
			input.AbsentThresholdPercentage,
			missingPunchBehavior,
			input.ManualCorrectionAllowed,
			input.GraceTime,
			input.MaxLateAllowedPerMonth,
			penaltyType,
			input.PenaltyCountThreshold,
			input.EarlyExitEnabled,
			input.EarlyExitGraceTime,
			input.OvertimeEnabled,
			overtimeRate,
			input.MinOvertimeMinutes,
			holidayWorkBehavior,
			input.SandwichRuleEnabled,
			input.PayrollPreviewEnabled);

		if (entity.IsDefault)
		{
			await ClearDefaultsAsync(entity.AcademicYearId, entity.Id);
		}

		await repository.UpdateAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("attendance", dto, "edited");
		await auditWriter.WriteAsync(
			entity.AcademicYearId,
			"attendance_policy",
			entity.Id,
			entity.Name,
			"edited",
			$"Edited attendance policy {entity.Name}.",
			changedFields: HrAuditWriter.Diff(before, dto, "lastModificationTime", "lastModifierId"));
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Clone)]
	public async Task<AttendancePolicyDto> CloneAsync(Guid id, HrPolicyCloneRequestDto input)
	{
		var source = await repository.GetAsync(id);
		var entity = new AttendancePolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			source.AcademicYearId,
			source.WorkCalendarPolicyId,
			string.IsNullOrWhiteSpace(input.Name) ? $"{source.Name} Copy" : input.Name!,
			HrPolicyStatus.Draft,
			false,
			source.FullDayThresholdPercentage,
			source.HalfDayThresholdPercentage,
			source.AbsentThresholdPercentage,
			source.MissingPunchBehavior,
			source.ManualCorrectionAllowed,
			source.GraceTime,
			source.MaxLateAllowedPerMonth,
			source.PenaltyType,
			source.PenaltyCountThreshold,
			source.EarlyExitEnabled,
			source.EarlyExitGraceTime,
			source.OvertimeEnabled,
			source.OvertimeRate,
			source.MinOvertimeMinutes,
			source.HolidayWorkBehavior,
			source.SandwichRuleEnabled,
			source.PayrollPreviewEnabled);

		await repository.InsertAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("attendance", dto, "cloned");
		await auditWriter.WriteAsync(
			entity.AcademicYearId,
			"attendance_policy",
			entity.Id,
			entity.Name,
			"cloned",
			$"Cloned attendance policy from {source.Name}.",
			context: new { sourcePolicyId = source.Id, sourcePolicyName = source.Name });
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Archive)]
	public async Task DeleteAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		var preview = await policySafetyAppService.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "attendance",
			PolicyId = id,
			AcademicYearId = entity.AcademicYearId,
			Action = "archive"
		});
		await confirmationService.EnsureConfirmedAsync(preview);
		entity.Archive();
		await repository.UpdateAsync(entity, autoSave: true);
		await versionAppService.CaptureAsync("attendance", HrPolicyMapping.ToDto(entity), "archived");
		await auditWriter.WriteAsync(entity.AcademicYearId, "attendance_policy", entity.Id, entity.Name, "archived", $"Archived attendance policy {entity.Name}.");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Archive)]
	public async Task<AttendancePolicyDto> UnarchiveAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		if (entity.Status != HrPolicyStatus.Archived)
		{
			return HrPolicyMapping.ToDto(entity);
		}
		entity.Unarchive();
		await repository.UpdateAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("attendance", dto, "unarchived");
		await auditWriter.WriteAsync(entity.AcademicYearId, "attendance_policy", entity.Id, entity.Name, "unarchived", $"Unarchived attendance policy {entity.Name} as draft.");
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.SetDefault)]
	public async Task<AttendancePolicyDto> SetDefaultAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		HrPolicyMapping.EnsureActive(entity.Status);
		var preview = await policySafetyAppService.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "attendance",
			PolicyId = id,
			AcademicYearId = entity.AcademicYearId,
			Action = "set-default"
		});
		await confirmationService.EnsureConfirmedAsync(preview);
		await ClearDefaultsAsync(entity.AcademicYearId, entity.Id);
		entity.SetDefault(true);
		await repository.UpdateAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("attendance", dto, "set-default");
		await auditWriter.WriteAsync(entity.AcademicYearId, "attendance_policy", entity.Id, entity.Name, "set_default", $"Set attendance policy {entity.Name} as default.");
		return dto;
	}

	private AttendancePolicy BuildEntity(Guid id, CreateUpdateAttendancePolicyDto input) =>
		new(
			id,
			CurrentTenant.Id,
			input.AcademicYearId,
			input.WorkCalendarPolicyId,
			input.Name,
			HrPolicyMapping.ParsePolicyStatus(input.Status),
			input.IsDefault,
			input.FullDayThresholdPercentage,
			input.HalfDayThresholdPercentage,
			input.AbsentThresholdPercentage,
			HrPolicyMapping.ParseMissingPunchBehavior(input.MissingPunchBehavior),
			input.ManualCorrectionAllowed,
			input.GraceTime,
			input.MaxLateAllowedPerMonth,
			HrPolicyMapping.ParsePenaltyType(input.PenaltyType),
			input.PenaltyCountThreshold,
			input.EarlyExitEnabled,
			input.EarlyExitGraceTime,
			input.OvertimeEnabled,
			HrPolicyMapping.ParseOvertimeRate(input.OvertimeRate),
			input.MinOvertimeMinutes,
			HrPolicyMapping.ParseHolidayWorkBehavior(input.HolidayWorkBehavior),
			input.SandwichRuleEnabled,
			input.PayrollPreviewEnabled);

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
			await versionAppService.CaptureAsync("attendance", HrPolicyMapping.ToDto(item), "set-default-cleared");
			await auditWriter.WriteAsync(item.AcademicYearId, "attendance_policy", item.Id, item.Name, "set_default", $"Cleared default attendance policy {item.Name}.");
		}
	}

	private static bool IsUnchanged(
		AttendancePolicy entity,
		CreateUpdateAttendancePolicyDto input,
		HrPolicyStatus status,
		AttendanceMissingPunchBehavior missingPunchBehavior,
		AttendancePenaltyType penaltyType,
		AttendanceOvertimeRate? overtimeRate,
		AttendanceHolidayWorkBehavior holidayWorkBehavior)
	{
		return entity.AcademicYearId == input.AcademicYearId &&
			entity.WorkCalendarPolicyId == input.WorkCalendarPolicyId &&
			string.Equals(entity.Name, input.Name.Trim(), StringComparison.Ordinal) &&
			entity.Status == status &&
			entity.IsDefault == input.IsDefault &&
			entity.FullDayThresholdPercentage == input.FullDayThresholdPercentage &&
			entity.HalfDayThresholdPercentage == input.HalfDayThresholdPercentage &&
			entity.AbsentThresholdPercentage == input.AbsentThresholdPercentage &&
			entity.MissingPunchBehavior == missingPunchBehavior &&
			entity.ManualCorrectionAllowed == input.ManualCorrectionAllowed &&
			entity.GraceTime == input.GraceTime &&
			entity.MaxLateAllowedPerMonth == input.MaxLateAllowedPerMonth &&
			entity.PenaltyType == penaltyType &&
			entity.PenaltyCountThreshold == input.PenaltyCountThreshold &&
			entity.EarlyExitEnabled == input.EarlyExitEnabled &&
			entity.EarlyExitGraceTime == input.EarlyExitGraceTime &&
			entity.OvertimeEnabled == input.OvertimeEnabled &&
			entity.OvertimeRate == overtimeRate &&
			entity.MinOvertimeMinutes == input.MinOvertimeMinutes &&
			entity.HolidayWorkBehavior == holidayWorkBehavior &&
			entity.SandwichRuleEnabled == input.SandwichRuleEnabled &&
			entity.PayrollPreviewEnabled == input.PayrollPreviewEnabled;
	}
}