using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
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
	IEmployeeAttendanceRepository employeeAttendanceRepository)
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
			warnings.Add(BuildWorkedTimeWarning(
				evaluatedStatus,
				workedMinutes,
				shiftMinutes,
				workedPercentage,
				policy.FullDayThresholdPercentage));
		}

		evaluatedStatus = await EvaluateLateArrivalPenaltyAsync(
			employee.Id,
			attendanceDate,
			policy,
			shift,
			timeIn.Value,
			evaluatedStatus,
			warnings,
			currentMonthPunchContext,
			cancellationToken);

		AddEarlyExitWarning(policy, shift, timeOut.Value, warnings);
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

	private async Task<EmployeeAttendanceStatusType> EvaluateLateArrivalPenaltyAsync(
		Guid employeeId,
		DateOnly attendanceDate,
		AttendancePolicy policy,
		ShiftManagement shift,
		DateTimeOffset timeIn,
		EmployeeAttendanceStatusType evaluatedStatus,
		List<string> warnings,
		IReadOnlyCollection<AttendancePolicyEvaluationPunchContext>? currentMonthPunchContext,
		CancellationToken cancellationToken)
	{
		var lateMinutes = (timeIn.TimeOfDay - shift.StartTime.ToTimeSpan()).TotalMinutes;
		if (lateMinutes <= policy.GraceTime)
		{
			return evaluatedStatus;
		}

		var monthlyLateCount = await CountMonthlyLateArrivalsAsync(
			employeeId,
			attendanceDate,
			shift,
			policy.GraceTime,
			currentMonthPunchContext,
			cancellationToken) + 1;
		var effectiveTrigger = Math.Max(
			policy.MaxLateAllowedPerMonth + 1,
			policy.PenaltyCountThreshold > 0 ? policy.PenaltyCountThreshold : 1);

		var penaltyStatus = policy.PenaltyType switch
		{
			AttendancePenaltyType.DeductHalfDay => EmployeeAttendanceStatusType.HalfDayLeave,
			AttendancePenaltyType.DeductFullDay => EmployeeAttendanceStatusType.FullDayLeave,
			_ => (EmployeeAttendanceStatusType?)null
		};

		if (policy.PenaltyType == AttendancePenaltyType.None)
		{
			warnings.Add(BuildLateArrivalNoPenaltyWarning(lateMinutes, policy.GraceTime, monthlyLateCount));
			return evaluatedStatus;
		}

		if (monthlyLateCount < effectiveTrigger)
		{
			warnings.Add(BuildLateArrivalBelowTriggerWarning(lateMinutes, policy.GraceTime, monthlyLateCount, effectiveTrigger));
			return evaluatedStatus;
		}

		if (policy.PenaltyType == AttendancePenaltyType.Warning)
		{
			warnings.Add(BuildLateArrivalWarningActionWarning(lateMinutes, policy.GraceTime, monthlyLateCount, effectiveTrigger));
			return evaluatedStatus;
		}

		if (policy.PenaltyType == AttendancePenaltyType.SalaryDeduction)
		{
			warnings.Add(BuildLateArrivalSalaryDeductionWarning(lateMinutes, policy.GraceTime, monthlyLateCount, effectiveTrigger));
			return evaluatedStatus;
		}

		if (!penaltyStatus.HasValue)
		{
			warnings.Add(BuildLateArrivalNoPenaltyWarning(lateMinutes, policy.GraceTime, monthlyLateCount));
			return evaluatedStatus;
		}

		var statusPenalty = penaltyStatus.Value;
		if (GetStatusSeverity(statusPenalty) <= GetStatusSeverity(evaluatedStatus))
		{
			warnings.Add(BuildLateArrivalAlreadyDowngradedWarning(
				lateMinutes,
				policy.GraceTime,
				monthlyLateCount,
				effectiveTrigger,
				evaluatedStatus));
			return evaluatedStatus;
		}

		if (monthlyLateCount >= effectiveTrigger)
		{
			warnings.Add(BuildLateArrivalPenaltyWarning(
				statusPenalty,
				lateMinutes,
				policy.GraceTime,
				monthlyLateCount,
				effectiveTrigger));
			return statusPenalty;
		}

		return evaluatedStatus;
	}

	private async Task<int> CountMonthlyLateArrivalsAsync(
		Guid employeeId,
		DateOnly attendanceDate,
		ShiftManagement shift,
		int graceTime,
		IReadOnlyCollection<AttendancePolicyEvaluationPunchContext>? currentMonthPunchContext,
		CancellationToken cancellationToken)
	{
		var monthStart = new DateOnly(attendanceDate.Year, attendanceDate.Month, 1);
		var rows = await employeeAttendanceRepository.GetListAsync(
			x => x.EmployeeId == employeeId &&
			     x.AttendanceDate >= monthStart &&
			     x.AttendanceDate < attendanceDate &&
			     x.TimeIn.HasValue,
			cancellationToken: cancellationToken);

		var currentBatchPunches = currentMonthPunchContext?
			.Where(x => x.AttendanceDate >= monthStart && x.AttendanceDate < attendanceDate)
			.ToList() ?? [];
		var currentBatchDates = currentBatchPunches
			.Select(x => x.AttendanceDate)
			.ToHashSet();
		var persistedLateCount = rows.Count(x =>
			!currentBatchDates.Contains(x.AttendanceDate) &&
			IsLateArrival(x.TimeIn!.Value, shift, graceTime));
		var currentBatchLateCount = currentBatchPunches.Count(x => IsLateArrival(x.TimeIn, shift, graceTime));

		return persistedLateCount + currentBatchLateCount;
	}

	private static void AddEarlyExitWarning(
		AttendancePolicy policy,
		ShiftManagement shift,
		DateTimeOffset timeOut,
		List<string> warnings)
	{
		if (!policy.EarlyExitEnabled)
		{
			return;
		}

		var earlyExitMinutes = (shift.EndTime.ToTimeSpan() - timeOut.TimeOfDay).TotalMinutes;
		if (earlyExitMinutes <= policy.EarlyExitGraceTime)
		{
			return;
		}

		warnings.Add(string.Format(
			CultureInfo.InvariantCulture,
			"Early exit warning: left {0:0.##} minutes before shift end, exceeding grace time {1} minutes.",
			earlyExitMinutes,
			policy.EarlyExitGraceTime));
	}

	private static bool IsLateArrival(DateTimeOffset timeIn, ShiftManagement shift, int graceTime) =>
		(timeIn.TimeOfDay - shift.StartTime.ToTimeSpan()).TotalMinutes > graceTime;

	private static int GetStatusSeverity(EmployeeAttendanceStatusType status) => status switch
	{
		EmployeeAttendanceStatusType.FullDayLeave => 3,
		EmployeeAttendanceStatusType.HalfDayLeave => 2,
		EmployeeAttendanceStatusType.Present => 1,
		_ => 0
	};

	private static string BuildWorkedTimeWarning(
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

	private static string BuildLateArrivalPenaltyWarning(
		EmployeeAttendanceStatusType evaluatedStatus,
		double lateMinutes,
		int graceTime,
		int monthlyLateCount,
		int effectiveTrigger)
	{
		var label = evaluatedStatus switch
		{
			EmployeeAttendanceStatusType.HalfDayLeave => "Half Day",
			EmployeeAttendanceStatusType.FullDayLeave => "Absent",
			_ => "Present"
		};

		return string.Format(
			CultureInfo.InvariantCulture,
			"Auto-marked {0}: late arrival {1:0.##} minutes exceeded grace time {2} minutes, monthly late count {3} reached trigger {4}.",
			label,
			lateMinutes,
			graceTime,
			monthlyLateCount,
			effectiveTrigger);
	}

	private static string BuildLateArrivalBelowTriggerWarning(
		double lateMinutes,
		int graceTime,
		int monthlyLateCount,
		int effectiveTrigger) =>
		string.Format(
			CultureInfo.InvariantCulture,
			"Late arrival warning: late arrival {0:0.##} minutes exceeded grace time {1} minutes; monthly late count {2} has not reached trigger {3}.",
			lateMinutes,
			graceTime,
			monthlyLateCount,
			effectiveTrigger);

	private static string BuildLateArrivalWarningActionWarning(
		double lateMinutes,
		int graceTime,
		int monthlyLateCount,
		int effectiveTrigger) =>
		string.Format(
			CultureInfo.InvariantCulture,
			"Late arrival warning action: late arrival {0:0.##} minutes exceeded grace time {1} minutes; monthly late count {2} reached trigger {3}.",
			lateMinutes,
			graceTime,
			monthlyLateCount,
			effectiveTrigger);

	private static string BuildLateArrivalSalaryDeductionWarning(
		double lateMinutes,
		int graceTime,
		int monthlyLateCount,
		int effectiveTrigger) =>
		string.Format(
			CultureInfo.InvariantCulture,
			"Late arrival salary deduction marker: late arrival {0:0.##} minutes exceeded grace time {1} minutes; monthly late count {2} reached trigger {3}.",
			lateMinutes,
			graceTime,
			monthlyLateCount,
			effectiveTrigger);

	private static string BuildLateArrivalNoPenaltyWarning(
		double lateMinutes,
		int graceTime,
		int monthlyLateCount) =>
		string.Format(
			CultureInfo.InvariantCulture,
			"Late arrival warning: late arrival {0:0.##} minutes exceeded grace time {1} minutes; monthly late count is {2}, with no late penalty configured.",
			lateMinutes,
			graceTime,
			monthlyLateCount);

	private static string BuildLateArrivalAlreadyDowngradedWarning(
		double lateMinutes,
		int graceTime,
		int monthlyLateCount,
		int effectiveTrigger,
		EmployeeAttendanceStatusType evaluatedStatus) =>
		string.Format(
			CultureInfo.InvariantCulture,
			"Late arrival warning: late arrival {0:0.##} minutes exceeded grace time {1} minutes and monthly late count {2} reached trigger {3}, but status stayed {4} because it was already equal or more severe.",
			lateMinutes,
			graceTime,
			monthlyLateCount,
			effectiveTrigger,
			evaluatedStatus);
}
