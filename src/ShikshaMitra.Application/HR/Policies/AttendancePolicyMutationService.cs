using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
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
	AttendancePolicyDefaultService defaultService,
	HrPolicyArchiveAssignmentRepairService archiveAssignmentRepairService,
	HrPolicyWorkCalendarValidator workCalendarValidator,
	AttendancePolicyHistoryWriter historyWriter)
	: ApplicationService
{
	public async Task<AttendancePolicyDto> CreateAsync(CreateUpdateAttendancePolicyDto input)
	{
		var entity = AttendancePolicyModelBuilder.Build(GuidGenerator.Create(), CurrentTenant.Id, input);
		HrPolicyStatusGuard.EnsureArchiveUsesArchiveWorkflow(entity.Status);
		await workCalendarValidator.EnsureUsableAsync(input.WorkCalendarPolicyId, input.AcademicYearId, entity.Status);
		if (entity.IsDefault)
		{
			HrPolicyStatusGuard.EnsureDefaultIsActive(entity.Status);
			await impactGuard.EnsureCreateDefaultConfirmedAsync(entity.AcademicYearId, input);
			await defaultService.ClearDefaultsAsync(entity.AcademicYearId);
		}

		await repository.InsertAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await historyWriter.CreatedAsync(dto);
		return dto;
	}

	public async Task<AttendancePolicyDto> UpdateAsync(Guid id, CreateUpdateAttendancePolicyDto input)
	{
		var entity = await repository.GetAsync(id);
		HrPolicyStatusGuard.EnsureEditable(entity.Status);
		EnsureAcademicYearUnchanged(entity.AcademicYearId, input.AcademicYearId);
		var status = HrPolicyMapping.ParsePolicyStatus(input.Status);
		HrPolicyStatusGuard.EnsureArchiveUsesArchiveWorkflow(status);
		await workCalendarValidator.EnsureUsableAsync(input.WorkCalendarPolicyId, entity.AcademicYearId, status);
		var captureMode = HrPolicyMapping.ParseCaptureMode(input.CaptureMode);
		var missingPunchBehavior = HrPolicyMapping.ParseMissingPunchBehavior(input.MissingPunchBehavior);
		var penaltyType = HrPolicyMapping.ParsePenaltyType(input.PenaltyType);
		var overtimeRate = HrPolicyMapping.ParseOvertimeRate(input.OvertimeRate);
		var holidayWorkBehavior = HrPolicyMapping.ParseHolidayWorkBehavior(input.HolidayWorkBehavior);
		var before = HrPolicyMapping.ToDto(entity);

		if (AttendancePolicyModelBuilder.IsUnchanged(
			entity,
			input,
			status,
			captureMode,
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

		if (input.IsDefault)
		{
			HrPolicyStatusGuard.EnsureDefaultIsActive(status);
		}
		await defaultService.EnsureDefaultRemainsAsync(entity, input.IsDefault, status);
		entity.Update(
			input.WorkCalendarPolicyId,
			input.Name,
			status,
			input.IsDefault,
			captureMode,
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
		await historyWriter.EditedAsync(before, dto);
		return dto;
	}

	public async Task<AttendancePolicyDto> CloneAsync(Guid id, HrPolicyCloneRequestDto input)
	{
		var source = await repository.GetAsync(id);
		await workCalendarValidator.EnsureUsableAsync(source.WorkCalendarPolicyId, source.AcademicYearId, HrPolicyStatus.Draft);
		var entity = new AttendancePolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			source.AcademicYearId,
			source.WorkCalendarPolicyId,
			string.IsNullOrWhiteSpace(input.Name) ? $"{source.Name} Copy" : input.Name!,
			HrPolicyStatus.Draft,
			false,
			source.CaptureMode,
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
		await historyWriter.ClonedAsync(dto, source);
		return dto;
	}

	public async Task DeleteAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		if (entity.Status == HrPolicyStatus.Archived)
		{
			return;
		}

		await defaultService.EnsureDefaultRemainsAsync(entity, isDefaultAfterUpdate: false, statusAfterUpdate: HrPolicyStatus.Archived);
		await impactGuard.EnsureArchiveConfirmedAsync(id, entity.AcademicYearId);
		await archiveAssignmentRepairService.ReassignAttendanceReferencesToDefaultAsync(entity);
		entity.Archive();
		await repository.UpdateAsync(entity, autoSave: true);
		await historyWriter.ArchivedAsync(HrPolicyMapping.ToDto(entity));
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
		await historyWriter.UnarchivedAsync(dto);
		return dto;
	}

	public async Task<AttendancePolicyDto> SetDefaultAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		HrPolicyMapping.EnsureActive(entity.Status);
		await workCalendarValidator.EnsureUsableAsync(entity.WorkCalendarPolicyId, entity.AcademicYearId, entity.Status);
		await impactGuard.EnsureSetDefaultConfirmedAsync(id, entity.AcademicYearId);
		await defaultService.ClearDefaultsAsync(entity.AcademicYearId, entity.Id);
		entity.SetDefault(true);
		await repository.UpdateAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await historyWriter.SetDefaultAsync(dto);
		return dto;
	}

	private static void EnsureAcademicYearUnchanged(Guid entityAcademicYearId, Guid inputAcademicYearId)
	{
		if (entityAcademicYearId != inputAcademicYearId)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch);
		}
	}

}
