using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.TenantSettings;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public sealed record AttendanceWorkedTimeEvaluation(
	EmployeeAttendanceStatusType Status,
	string? Warning);

public class AttendancePolicyWorkedTimeEvaluator(
	HrPolicyResolver policyResolver,
	IRepository<ShiftManagement, Guid> shiftRepository)
	: ITransientDependency
{
	public async Task<AttendanceWorkedTimeEvaluation> EvaluateAsync(
		Employee employee,
		DateOnly attendanceDate,
		DateTimeOffset? timeIn,
		DateTimeOffset? timeOut,
		EmployeeAttendanceStatusType requestedStatus,
		CancellationToken cancellationToken = default)
	{
		var hasTimeIn = timeIn.HasValue;
		var hasTimeOut = timeOut.HasValue;
		if (hasTimeIn != hasTimeOut)
		{
			throw new BusinessException(
				HRErrorCodes.AttendancePolicyEvaluationInvalidPunches,
				"Time In and Time Out must both be provided before attendance policy can evaluate worked time.");
		}

		if (!hasTimeIn && !hasTimeOut)
		{
			if (requestedStatus == EmployeeAttendanceStatusType.Present)
			{
				throw new BusinessException(
					HRErrorCodes.AttendancePolicyEvaluationInvalidPunches,
					"Present attendance requires both Time In and Time Out.");
			}

			return new AttendanceWorkedTimeEvaluation(requestedStatus, null);
		}

		if (timeOut!.Value <= timeIn!.Value)
		{
			throw new BusinessException(
				HRErrorCodes.AttendancePolicyEvaluationInvalidPunches,
				"Time Out must be after Time In.");
		}

		if (!employee.ShiftId.HasValue)
		{
			throw new BusinessException(
				HRErrorCodes.AttendancePolicyEvaluationShiftRequired,
				"Employee shift is required to evaluate worked-time attendance policy.");
		}

		var shift = await shiftRepository.FindAsync(employee.ShiftId.Value, cancellationToken: cancellationToken)
			?? throw new BusinessException(
				HRErrorCodes.AttendancePolicyEvaluationShiftRequired,
				"Employee shift was not found for worked-time attendance policy evaluation.");

		var shiftMinutes = (shift.EndTime.ToTimeSpan() - shift.StartTime.ToTimeSpan()).TotalMinutes;
		if (shiftMinutes <= 0)
		{
			throw new BusinessException(
				HRErrorCodes.AttendancePolicyEvaluationInvalidShift,
				"Employee shift start and end time must define a positive duration.");
		}

		var policies = await policyResolver.ResolveAsync(
			employee.Id,
			attendanceDate.ToDateTime(TimeOnly.MinValue),
			cancellationToken: cancellationToken);
		var policy = policies.AttendancePolicy;

		if (policy.AbsentThresholdPercentage > policy.HalfDayThresholdPercentage ||
		    policy.HalfDayThresholdPercentage >= policy.FullDayThresholdPercentage)
		{
			throw new BusinessException(
				HRErrorCodes.AttendancePolicyEvaluationInvalidPolicy,
				"Attendance policy thresholds are invalid. Please review full-day, half-day, and absent thresholds.");
		}

		var workedMinutes = (timeOut.Value - timeIn.Value).TotalMinutes;
		var workedPercentage = workedMinutes / shiftMinutes * 100d;
		var evaluatedStatus = workedPercentage >= policy.FullDayThresholdPercentage
			? EmployeeAttendanceStatusType.Present
			: workedPercentage >= policy.AbsentThresholdPercentage
				? EmployeeAttendanceStatusType.HalfDayLeave
				: EmployeeAttendanceStatusType.FullDayLeave;

		var workedTimeWarning = evaluatedStatus == requestedStatus
			? null
			: BuildWarning(evaluatedStatus, workedMinutes, shiftMinutes, workedPercentage, policy.FullDayThresholdPercentage);

		var latePenalty = EvaluateLateArrivalPenalty(policy, shift, timeIn.Value, evaluatedStatus);
		if (latePenalty != null)
		{
			return latePenalty;
		}

		var warning = workedTimeWarning;
		return new AttendanceWorkedTimeEvaluation(evaluatedStatus, warning);
	}

	private static AttendanceWorkedTimeEvaluation? EvaluateLateArrivalPenalty(
		AttendancePolicy policy,
		ShiftManagement shift,
		DateTimeOffset timeIn,
		EmployeeAttendanceStatusType evaluatedStatus)
	{
		var penaltyStatus = policy.PenaltyType switch
		{
			AttendancePenaltyType.DeductHalfDay => EmployeeAttendanceStatusType.HalfDayLeave,
			AttendancePenaltyType.DeductFullDay => EmployeeAttendanceStatusType.FullDayLeave,
			_ => (EmployeeAttendanceStatusType?)null
		};

		if (!penaltyStatus.HasValue || GetStatusSeverity(penaltyStatus.Value) <= GetStatusSeverity(evaluatedStatus))
		{
			return null;
		}

		var lateMinutes = (timeIn.TimeOfDay - shift.StartTime.ToTimeSpan()).TotalMinutes;
		if (lateMinutes <= policy.GraceTime)
		{
			return null;
		}

		return new AttendanceWorkedTimeEvaluation(
			penaltyStatus.Value,
			BuildLateArrivalWarning(penaltyStatus.Value, lateMinutes, policy.GraceTime));
	}

	private static int GetStatusSeverity(EmployeeAttendanceStatusType status) => status switch
	{
		EmployeeAttendanceStatusType.FullDayLeave => 3,
		EmployeeAttendanceStatusType.HalfDayLeave => 2,
		EmployeeAttendanceStatusType.Present => 1,
		_ => 0
	};

	private static string BuildWarning(
		EmployeeAttendanceStatusType evaluatedStatus,
		double workedMinutes,
		double shiftMinutes,
		double workedPercentage,
		double fullDayThresholdPercentage)
	{
		var label = evaluatedStatus switch
		{
			EmployeeAttendanceStatusType.HalfDayLeave => "Half Day",
			EmployeeAttendanceStatusType.FullDayLeave => "Absent",
			_ => "Present"
		};

		return string.Format(
			CultureInfo.InvariantCulture,
			"Auto-marked {0}: worked {1:0.##}h of {2:0.##}h shift ({3:0.##}%), below full-day threshold {4:0.##}%.",
			label,
			workedMinutes / 60d,
			shiftMinutes / 60d,
			workedPercentage,
			fullDayThresholdPercentage);
	}

	private static string BuildLateArrivalWarning(
		EmployeeAttendanceStatusType evaluatedStatus,
		double lateMinutes,
		int graceTime)
	{
		var label = evaluatedStatus switch
		{
			EmployeeAttendanceStatusType.HalfDayLeave => "Half Day",
			EmployeeAttendanceStatusType.FullDayLeave => "Absent",
			_ => "Present"
		};

		return string.Format(
			CultureInfo.InvariantCulture,
			"Auto-marked {0}: late arrival {1:0.##} minutes exceeded grace time {2} minutes.",
			label,
			lateMinutes,
			graceTime);
	}
}
