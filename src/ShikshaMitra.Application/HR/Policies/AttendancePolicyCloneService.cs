using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

public class AttendancePolicyCloneService(
	IRepository<AttendancePolicy, Guid> repository,
	HrPolicyWorkCalendarValidator workCalendarValidator,
	AttendancePolicyHistoryWriter historyWriter,
	HrPolicyNameUniquenessService nameUniquenessService)
	: ApplicationService, ITransientDependency
{
	public async Task<AttendancePolicyDto> CloneAsync(
		Guid id,
		HrPolicyCloneRequestDto input)
	{
		var source = await repository.GetAsync(id);
		await workCalendarValidator.EnsureUsableAsync(
			source.WorkCalendarPolicyId,
			source.AcademicYearId,
			HrPolicyStatus.Draft);
		var cloneName = string.IsNullOrWhiteSpace(input.Name)
			? $"{source.Name} Copy"
			: input.Name!;
		await nameUniquenessService.EnsureAttendanceAvailableAsync(
			source.AcademicYearId,
			cloneName);
		var entity = new AttendancePolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			source.AcademicYearId,
			source.WorkCalendarPolicyId,
			cloneName,
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
			source.PayrollPreviewEnabled,
			source.PenaltyDeductionAmount);

		await nameUniquenessService.ExecuteAttendanceWriteAsync(
			entity.Name,
			() => repository.InsertAsync(entity, autoSave: true));
		var dto = HrPolicyMapping.ToDto(entity);
		await historyWriter.ClonedAsync(dto, source);
		return dto;
	}
}
