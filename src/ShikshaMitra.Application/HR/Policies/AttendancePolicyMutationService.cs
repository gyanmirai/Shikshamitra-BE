using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Audit;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class AttendancePolicyMutationService(
	IRepository<AttendancePolicy, Guid> repository,
	AttendancePolicyImpactGuard impactGuard,
	HrPolicyVersionAppService versionAppService,
	AttendancePolicyDefaultService defaultService,
	HrPolicyWorkCalendarValidator workCalendarValidator,
	HrAuditWriter auditWriter)
	: ApplicationService
{
	public async Task<AttendancePolicyDto> CreateAsync(CreateUpdateAttendancePolicyDto input)
	{
		await workCalendarValidator.EnsureUsableAsync(input.WorkCalendarPolicyId, input.AcademicYearId);
		var entity = AttendancePolicyModelBuilder.Build(GuidGenerator.Create(), CurrentTenant.Id, input);
		if (entity.IsDefault)
		{
			await defaultService.ClearDefaultsAsync(entity.AcademicYearId);
		}

		await repository.InsertAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("attendance", dto, "created");
		await auditWriter.WriteAsync(entity.AcademicYearId, "attendance_policy", entity.Id, entity.Name, "created", $"Created attendance policy {entity.Name}.");
		return dto;
	}

	public async Task<AttendancePolicyDto> UpdateAsync(Guid id, CreateUpdateAttendancePolicyDto input)
	{
		await workCalendarValidator.EnsureUsableAsync(input.WorkCalendarPolicyId, input.AcademicYearId);
		var entity = await repository.GetAsync(id);
		var status = HrPolicyMapping.ParsePolicyStatus(input.Status);
		var missingPunchBehavior = HrPolicyMapping.ParseMissingPunchBehavior(input.MissingPunchBehavior);
		var penaltyType = HrPolicyMapping.ParsePenaltyType(input.PenaltyType);
		var overtimeRate = HrPolicyMapping.ParseOvertimeRate(input.OvertimeRate);
		var holidayWorkBehavior = HrPolicyMapping.ParseHolidayWorkBehavior(input.HolidayWorkBehavior);
		var before = HrPolicyMapping.ToDto(entity);

		if (AttendancePolicyModelBuilder.IsUnchanged(
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
			await impactGuard.EnsureUpdateConfirmedAsync(id, entity.AcademicYearId, input);
		}

		await defaultService.EnsureDefaultRemainsAsync(entity, input.IsDefault, status);
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
			await defaultService.ClearDefaultsAsync(entity.AcademicYearId, entity.Id);
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

	public async Task DeleteAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		await defaultService.EnsureDefaultRemainsAsync(entity, isDefaultAfterUpdate: false, statusAfterUpdate: HrPolicyStatus.Archived);
		await impactGuard.EnsureArchiveConfirmedAsync(id, entity.AcademicYearId);
		entity.Archive();
		await repository.UpdateAsync(entity, autoSave: true);
		await versionAppService.CaptureAsync("attendance", HrPolicyMapping.ToDto(entity), "archived");
		await auditWriter.WriteAsync(entity.AcademicYearId, "attendance_policy", entity.Id, entity.Name, "archived", $"Archived attendance policy {entity.Name}.");
	}

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

	public async Task<AttendancePolicyDto> SetDefaultAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		HrPolicyMapping.EnsureActive(entity.Status);
		await impactGuard.EnsureSetDefaultConfirmedAsync(id, entity.AcademicYearId);
		await defaultService.ClearDefaultsAsync(entity.AcademicYearId, entity.Id);
		entity.SetDefault(true);
		await repository.UpdateAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("attendance", dto, "set-default");
		await auditWriter.WriteAsync(entity.AcademicYearId, "attendance_policy", entity.Id, entity.Name, "set_default", $"Set attendance policy {entity.Name} as default.");
		return dto;
	}

}
