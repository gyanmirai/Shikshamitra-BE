using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Statutory;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public static class PayrollLockPreconditionGuard
{
	public static void EnsureNoFinalizedSlips(
		IReadOnlyCollection<Guid> employeeIds,
		IReadOnlyCollection<SalarySlip> existingSlips,
		string month)
	{
		var finalizedEmployeeIds = existingSlips
			.Where(x => x.Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid)
			.Select(x => x.EmployeeId)
			.ToHashSet();
		var alreadyFinalizedIds = employeeIds
			.Where(finalizedEmployeeIds.Contains)
			.ToList();
		if (alreadyFinalizedIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollAlreadyLocked)
				.WithData("month", month)
				.WithData("employeeIds", alreadyFinalizedIds);
		}
	}

	public static void EnsureReviewed(
		IReadOnlyCollection<Guid> employeeIds,
		IReadOnlyDictionary<Guid, PayrollRunItemSnapshot> snapshotByEmployee,
		string month)
	{
		var unreviewedEmployeeIds = employeeIds
			.Where(employeeId => !snapshotByEmployee.TryGetValue(employeeId, out var snapshot) ||
								 snapshot.IsLocked ||
								 snapshot.ReviewStatus != ReviewStatuses.Reviewed)
			.ToList();
		if (unreviewedEmployeeIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollReviewRequiredBeforeLock)
				.WithData("month", month)
				.WithData("employeeIds", unreviewedEmployeeIds);
		}
	}

	public static void EnsureNoBlockingSetupIssues(
		IReadOnlyCollection<Guid> employeeIds,
		IReadOnlyDictionary<Guid, PayrollRunItemSnapshot> snapshotByEmployee,
		string month)
	{
		var blockedBySetupIds = employeeIds
			.Where(employeeId => snapshotByEmployee.TryGetValue(employeeId, out var snapshot) &&
				HasBlockingLockIssue(snapshot.IssueCodesJson))
			.ToList();
		if (blockedBySetupIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollBlockingIssuesBeforeLock)
				.WithData("month", month)
				.WithData("employeeIds", blockedBySetupIds);
		}
	}

	public static bool HasBlockingLockIssue(string issuesJson)
	{
		var issues = JsonSerializer.Deserialize<List<string>>(issuesJson) ?? new List<string>();
		return issues.Any(IsBlockingLockIssue);
	}

	public static IReadOnlyList<string> BlockingIssueCodes { get; } = Array.AsReadOnly(new[]
	{
		IssueCodes.NoAttendance, IssueCodes.MissingSalaryStructure, IssueCodes.NegativeNetPay,
		IssueCodes.AttendanceSalaryDeductionConfiguration, IssueCodes.PayrollPolicyConfiguration,
		"PF_WAGE_BASIS_REQUIRED", "PF_MEMBERSHIP_REVIEW_REQUIRED", "PF_ENROLLMENT_REQUIRED", "PF_CONSENT_REQUIRED",
		"PF_ESTABLISHMENT_NOT_COVERED", "ESI_BASIC_COMPONENT_REQUIRED", "ESI_COMPONENT_REQUIRED",
		IssueCodes.NoWorkingDays, IssueCodes.EsiEstablishmentInactive, IssueCodes.EsiWageClassificationRequired,
		IssueCodes.EsiEnrollmentReviewRequired, IssueCodes.CompensationRevisionRequired, IssueCodes.CompensationTargetVariance
	});

	public static bool IsBlockingLockIssue(string issue) => BlockingIssueCodes.Contains(issue);
}
