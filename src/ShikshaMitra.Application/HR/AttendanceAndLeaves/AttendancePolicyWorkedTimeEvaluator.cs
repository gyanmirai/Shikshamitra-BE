using System;
using System.Collections.Generic;
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
	IReadOnlyList<string> Warnings);

public sealed record AttendancePolicyEvaluationPunchContext(
	DateOnly AttendanceDate,
	DateTimeOffset TimeIn);

public class AttendancePolicyWorkedTimeEvaluator(
	HrPolicyResolver policyResolver,
	IRepository<ShiftManagement, Guid> shiftRepository,
	AttendancePolicyLateArrivalEvaluator lateArrivalEvaluator)
	: ITransientDependency
{
	public async Task<AttendanceWorkedTimeEvaluation> EvaluateAsync(
		Employee employee,
		DateOnly attendanceDate,
		DateTimeOffset? timeIn,
		DateTimeOffset? timeOut,
		EmployeeAttendanceStatusType requestedStatus,
		IReadOnlyCollection<AttendancePolicyEvaluationPunchContext>? currentMonthPunchContext = null,
		CancellationToken cancellationToken = default)
	{
		var hasTimeIn = timeIn.HasValue;
		var hasTimeOut = timeOut.HasValue;

		if (!hasTimeIn && !hasTimeOut)
		{
			if (requestedStatus == EmployeeAttendanceStatusType.Present)
			{
				throw new BusinessException(
					HRErrorCodes.AttendancePolicyEvaluationInvalidPunches,
					"Present attendance requires both Time In and Time Out.");
			}

			return new AttendanceWorkedTimeEvaluation(requestedStatus, []);
		}

		var policies = await policyResolver.ResolveAsync(
			employee.Id,
			attendanceDate.ToDateTime(TimeOnly.MinValue),
			cancellationToken: cancellationToken);
		var policy = policies.AttendancePolicy;

		if (hasTimeIn != hasTimeOut)
		{
			return EvaluateMissingPunch(policy);
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

		var warnings = new List<string>();
		if (evaluatedStatus != requestedStatus)
		{
			warnings.Add(AttendancePolicyWorkedTimeWarningBuilder.BuildWorkedTimeWarning(
				evaluatedStatus,
				workedMinutes,
				shiftMinutes,
				workedPercentage,
				policy.FullDayThresholdPercentage));
		}

		evaluatedStatus = await lateArrivalEvaluator.EvaluateAsync(
			employee.Id,
			attendanceDate,
			policy,
			shift,
			timeIn.Value,
			evaluatedStatus,
			warnings,
			currentMonthPunchContext,
			cancellationToken);

		AttendancePolicyWorkedTimeWarningBuilder.AddEarlyExitWarning(policy, shift, timeOut.Value, warnings);
		return new AttendanceWorkedTimeEvaluation(evaluatedStatus, warnings);
	}

	private static AttendanceWorkedTimeEvaluation EvaluateMissingPunch(AttendancePolicy policy)
	{
		return policy.MissingPunchBehavior switch
		{
			AttendanceMissingPunchBehavior.MarkAbsent => new AttendanceWorkedTimeEvaluation(
				EmployeeAttendanceStatusType.FullDayLeave,
				["Auto-marked Absent: missing punch policy applied because only one punch was provided."]),
			AttendanceMissingPunchBehavior.MarkHalfDay => new AttendanceWorkedTimeEvaluation(
				EmployeeAttendanceStatusType.HalfDayLeave,
				["Auto-marked Half Day: missing punch policy applied because only one punch was provided."]),
			_ => throw new BusinessException(
				HRErrorCodes.AttendancePolicyEvaluationInvalidPunches,
				"Time In and Time Out must both be provided before attendance policy can evaluate worked time.")
		};
	}
}
