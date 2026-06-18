using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendancePolicyLateArrivalEvaluator(
	IEmployeeAttendanceRepository employeeAttendanceRepository)
	: ITransientDependency
{
	public async Task<EmployeeAttendanceStatusType> EvaluateAsync(
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
		var effectiveTrigger = AttendancePolicyLateArrivalPenaltyRules.GetEffectiveTrigger(policy);

		var penaltyStatus = policy.PenaltyType switch
		{
			AttendancePenaltyType.DeductHalfDay => EmployeeAttendanceStatusType.HalfDayLeave,
			AttendancePenaltyType.DeductFullDay => EmployeeAttendanceStatusType.FullDayLeave,
			_ => (EmployeeAttendanceStatusType?)null
		};

		if (policy.PenaltyType == AttendancePenaltyType.None)
		{
			warnings.Add(AttendancePolicyWorkedTimeWarningBuilder.BuildLateArrivalNoPenaltyWarning(lateMinutes, policy.GraceTime, monthlyLateCount));
			return evaluatedStatus;
		}

		if (monthlyLateCount < effectiveTrigger)
		{
			warnings.Add(AttendancePolicyWorkedTimeWarningBuilder.BuildLateArrivalBelowTriggerWarning(lateMinutes, policy.GraceTime, monthlyLateCount, effectiveTrigger));
			return evaluatedStatus;
		}

		if (policy.PenaltyType == AttendancePenaltyType.Warning)
		{
			warnings.Add(AttendancePolicyWorkedTimeWarningBuilder.BuildLateArrivalWarningActionWarning(lateMinutes, policy.GraceTime, monthlyLateCount, effectiveTrigger));
			return evaluatedStatus;
		}

		if (policy.PenaltyType == AttendancePenaltyType.SalaryDeduction)
		{
			warnings.Add(AttendancePolicyWorkedTimeWarningBuilder.BuildLateArrivalSalaryDeductionWarning(lateMinutes, policy.GraceTime, monthlyLateCount, effectiveTrigger));
			return evaluatedStatus;
		}

		if (!penaltyStatus.HasValue)
		{
			warnings.Add(AttendancePolicyWorkedTimeWarningBuilder.BuildLateArrivalNoPenaltyWarning(lateMinutes, policy.GraceTime, monthlyLateCount));
			return evaluatedStatus;
		}

		var statusPenalty = penaltyStatus.Value;
		if (GetStatusSeverity(statusPenalty) <= GetStatusSeverity(evaluatedStatus))
		{
			warnings.Add(AttendancePolicyWorkedTimeWarningBuilder.BuildLateArrivalAlreadyDowngradedWarning(
				lateMinutes,
				policy.GraceTime,
				monthlyLateCount,
				effectiveTrigger,
				evaluatedStatus));
			return evaluatedStatus;
		}

		if (monthlyLateCount >= effectiveTrigger)
		{
			warnings.Add(AttendancePolicyWorkedTimeWarningBuilder.BuildLateArrivalPenaltyWarning(
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
			AttendancePolicyLateArrivalPenaltyRules.IsLateArrival(x.TimeIn!.Value, shift, graceTime));
		var currentBatchLateCount = currentBatchPunches.Count(x =>
			AttendancePolicyLateArrivalPenaltyRules.IsLateArrival(x.TimeIn, shift, graceTime));

		return persistedLateCount + currentBatchLateCount;
	}

	private static int GetStatusSeverity(EmployeeAttendanceStatusType status) => status switch
	{
		EmployeeAttendanceStatusType.FullDayLeave => 3,
		EmployeeAttendanceStatusType.HalfDayLeave => 2,
		EmployeeAttendanceStatusType.Present => 1,
		_ => 0
	};
}
