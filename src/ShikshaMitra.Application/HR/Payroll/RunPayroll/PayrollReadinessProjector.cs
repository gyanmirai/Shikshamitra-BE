using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

internal static class PayrollReadinessProjector
{
	public static RunPayrollReadinessSummary BuildSummary(
		string month,
		List<RunPayrollItem> items,
		bool isGenerated,
		int? employeeCountOverride = null,
		int? lockedCountOverride = null)
	{
		var missingAttendanceCount = items.Count(x => x.IssueCodes.Contains(IssueCodes.NoAttendance));
		var missingSalaryStructureCount = items.Count(x => x.IssueCodes.Contains(IssueCodes.MissingSalaryStructure));
		var policyConfigurationIssueCount = items.Count(x =>
			x.IssueCodes.Contains(IssueCodes.PayrollPolicyConfiguration) ||
			x.IssueCodes.Contains(IssueCodes.AttendanceSalaryDeductionConfiguration));
		var reviewedCount = items.Count(x => x.ReviewStatus == ReviewStatuses.Reviewed);
		var lockedCount = lockedCountOverride ?? items.Count(x => x.IsLocked || x.ReviewStatus == ReviewStatuses.Locked);
		var employeeCount = employeeCountOverride ?? items.Count;
		var blockingIssues = new List<string>();
		var recommendedActions = new List<string>();

		if (missingAttendanceCount > 0)
		{
			blockingIssues.Add(IssueCodes.NoAttendance);
			recommendedActions.Add("review-attendance");
			recommendedActions.Add("import-attendance");
		}

		if (missingSalaryStructureCount > 0)
		{
			blockingIssues.Add(IssueCodes.MissingSalaryStructure);
			recommendedActions.Add("assign-salary-structure");
		}

		if (policyConfigurationIssueCount > 0)
		{
			if (items.Any(x => x.IssueCodes.Contains(IssueCodes.PayrollPolicyConfiguration)))
			{
				blockingIssues.Add(IssueCodes.PayrollPolicyConfiguration);
				recommendedActions.Add("fix-payroll-policy");
			}

			if (items.Any(x => x.IssueCodes.Contains(IssueCodes.AttendanceSalaryDeductionConfiguration)))
			{
				blockingIssues.Add(IssueCodes.AttendanceSalaryDeductionConfiguration);
				recommendedActions.Add("fix-attendance-policy");
			}
		}

		if (items.Any(x => x.IssueCodes.Contains(IssueCodes.NoWorkingDays)))
		{
			blockingIssues.Add(IssueCodes.NoWorkingDays);
			recommendedActions.Add("review-work-calendar");
		}

		if (items.Any(x => x.IssueCodes.Contains(IssueCodes.NegativeNetPay)))
		{
			blockingIssues.Add(IssueCodes.NegativeNetPay);
			recommendedActions.Add("review-payroll-rows");
		}

		if (items.Any(x => x.IssueCodes.Contains(IssueCodes.CompensationRevisionRequired)))
		{
			blockingIssues.Add(IssueCodes.CompensationRevisionRequired);
			recommendedActions.Add("set-up-salary");
		}

		if (items.Any(x => x.IssueCodes.Contains(IssueCodes.CompensationTargetVariance)))
		{
			blockingIssues.Add(IssueCodes.CompensationTargetVariance);
			recommendedActions.Add("review-protected-target");
		}

		return new RunPayrollReadinessSummary
		{
			NetPayTotal = items.Sum(x => x.NetPay),
			Month = month,
			CanPreview = true,
			CanLock = !isGenerated && items.Count > 0 && items.Count == employeeCount && blockingIssues.Count == 0 && reviewedCount == employeeCount,
			EmployeeCount = employeeCount,
			SnapshotCount = items.Count,
			ReviewedCount = reviewedCount,
			LockedCount = lockedCount,
			AttendanceFoundCount = items.Count - missingAttendanceCount,
			MissingAttendanceCount = missingAttendanceCount,
			MissingSalaryStructureCount = missingSalaryStructureCount,
			PolicyConfigurationIssueCount = policyConfigurationIssueCount,
			BlockingIssues = blockingIssues.Distinct().ToList(),
			RecommendedActions = recommendedActions.Distinct().ToList()
		};
	}

	public static bool HasBlockingLockIssue(string issuesJson)
	{
		return PayrollLockPreconditionGuard.HasBlockingLockIssue(issuesJson);
	}
}
