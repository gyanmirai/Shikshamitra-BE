using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
public class AttendanceConfigurationAppService(
	IAttendanceConfigurationRepository attendanceConfigurationRepository)
	: ApplicationService, IAttendanceConfigurationAppService
{
	public async Task<AttendanceConfigurationResponse> CreateOrUpdateAsync(Guid academicYearId, AttendanceConfigurationRequest input)
	{
		var penaltyType = ParsePenaltyType(input.LateEarlyRules.PenaltyType);
		var overtimeRate = ParseOvertimeRate(input.Overtime.Rate);

		if (input.Overtime.Enabled && (!overtimeRate.HasValue || !input.Overtime.MinOvertimeMinutes.HasValue))
		{
			throw new BusinessException("ShikshaMitra:OvertimeRateAndMinMinutesRequired");
		}

		var existing = await attendanceConfigurationRepository.FindByAcademicYearIdAsync(academicYearId);
		if (existing == null)
		{
			var entity = new AttendanceConfiguration(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				academicYearId,
				input.DailyRules.HoursBasedPayPercentage,
				input.LateEarlyRules.GraceTime,
				input.LateEarlyRules.MaxLateAllowedPerMonth,
				penaltyType,
				input.Overtime.Enabled,
				overtimeRate,
				input.Overtime.MinOvertimeMinutes);

			await attendanceConfigurationRepository.InsertAsync(entity, autoSave: true);
		}
		else
		{
			existing.Update(
				input.DailyRules.HoursBasedPayPercentage,
				input.LateEarlyRules.GraceTime,
				input.LateEarlyRules.MaxLateAllowedPerMonth,
				penaltyType,
				input.Overtime.Enabled,
				overtimeRate,
				input.Overtime.MinOvertimeMinutes);

			await attendanceConfigurationRepository.UpdateAsync(existing, autoSave: true);
		}

		return new AttendanceConfigurationResponse
		{
			Success = true,
			Message = "Attendance configuration saved successfully"
		};
	}

	private static AttendancePenaltyType ParsePenaltyType(string value) => value switch
	{
		"deduct_half_day" => AttendancePenaltyType.DeductHalfDay,
		"deduct_full_day" => AttendancePenaltyType.DeductFullDay,
		_ => AttendancePenaltyType.None
	};

	private static AttendanceOvertimeRate? ParseOvertimeRate(string? value) => value switch
	{
		"1.5x" => AttendanceOvertimeRate.OnePointFiveX,
		"2.0x" => AttendanceOvertimeRate.TwoX,
		"flat" => AttendanceOvertimeRate.Flat,
		_ => null
	};
}
