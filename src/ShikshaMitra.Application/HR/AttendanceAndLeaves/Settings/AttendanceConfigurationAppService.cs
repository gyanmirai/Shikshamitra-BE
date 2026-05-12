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
	public async Task<AttendanceConfigurationResponse> GetAsync(Guid academicYearId)
	{
		var existing = await attendanceConfigurationRepository.FindByAcademicYearIdAsync(academicYearId);

		return existing == null
			? new AttendanceConfigurationResponse
			{
				Success = true,
				Message = "Attendance configuration not configured"
			}
			: MapToResponse(existing, "Attendance configuration fetched successfully");
	}

	public async Task<AttendanceConfigurationResponse> CreateOrUpdateAsync(Guid academicYearId, AttendanceConfigurationRequest input)
	{
		var penaltyType = ParsePenaltyType(input.LateEarlyRules.PenaltyType);
		var overtimeRate = ParseOvertimeRate(input.Overtime.Rate);
		AttendanceConfiguration saved;

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
			saved = entity;
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
			saved = existing;
		}

		return MapToResponse(saved, "Attendance configuration saved successfully");
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

	private static string MapPenaltyType(AttendancePenaltyType value) => value switch
	{
		AttendancePenaltyType.DeductHalfDay => "deduct_half_day",
		AttendancePenaltyType.DeductFullDay => "deduct_full_day",
		_ => "none"
	};

	private static string? MapOvertimeRate(AttendanceOvertimeRate? value) => value switch
	{
		AttendanceOvertimeRate.OnePointFiveX => "1.5x",
		AttendanceOvertimeRate.TwoX => "2.0x",
		AttendanceOvertimeRate.Flat => "flat",
		_ => null
	};

	private static AttendanceConfigurationResponse MapToResponse(AttendanceConfiguration entity, string message) =>
		new()
		{
			Success = true,
			Message = message,
			DailyRules = new AttendanceDailyRulesRequest
			{
				HoursBasedPayPercentage = entity.HoursBasedPayPercentage
			},
			LateEarlyRules = new AttendanceLateEarlyRulesRequest
			{
				GraceTime = entity.GraceTime,
				MaxLateAllowedPerMonth = entity.MaxLateAllowedPerMonth,
				PenaltyType = MapPenaltyType(entity.PenaltyType)
			},
			Overtime = new AttendanceOvertimeRulesRequest
			{
				Enabled = entity.OvertimeEnabled,
				Rate = MapOvertimeRate(entity.OvertimeRate),
				MinOvertimeMinutes = entity.MinOvertimeMinutes
			}
		};
}
