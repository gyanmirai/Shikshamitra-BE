using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

internal record PayrollVerifiedRow(PayrollPreviewEmployee Preview, RunPayrollItem Item,
	PayrollRunItemSnapshot Snapshot, string Digest);

public class PayrollSubmissionEvidence(
	IRepository<PayrollPreviewRun, Guid> runs,
	IRepository<PayrollPreviewEmployee, Guid> previewRows,
	IRepository<PayrollRunItemSnapshot, Guid> snapshots,
	IRepository<PayrollRunReview, Guid> reviews,
	IEmployeeRepository employees,
	IEmployeePayrollDetailsRepository bankDetails,
	ISalarySlipRepository slips,
	PayrollPreviewRunAppService previews,
	IDataProtectionProvider protection,
	IAsyncQueryableExecuter queries) : ITransientDependency
{
	private readonly IDataProtector protector = protection.CreateProtector("ShikshaMitra.PayrollSubmissionEvidence.v1");

	internal async Task<List<PayrollVerifiedRow>?> ReadCurrentAsync(Guid tenantId, Guid runId, Guid revision,
		string month, Guid? academicYearId, IReadOnlyCollection<Guid> ids)
	{
		var run = await runs.FindAsync(runId);
		if (run == null || run.TenantId != tenantId || run.Month != month || run.AcademicYearId != academicYearId ||
			run.Status != "SUCCEEDED" || !run.HasCurrentCalculationVersion || run.InputRevision != revision ||
			!await previews.IsLatestCompletedRunAsync(run))
			return null;

		var saved = await queries.ToListAsync((await previewRows.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && x.RunId == runId && ids.Contains(x.EmployeeId)));
		var monthly = (await queries.ToListAsync((await snapshots.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && x.MonthYear == month && ids.Contains(x.EmployeeId)))).ToDictionary(x => x.EmployeeId);
		var marks = (await queries.ToListAsync((await reviews.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && x.MonthYear == month && x.PreviewRunId == runId && ids.Contains(x.EmployeeId))))
			.ToDictionary(x => x.EmployeeId);
		var staff = (await queries.ToListAsync((await employees.GetQueryableAsync()).Where(x => ids.Contains(x.Id))))
			.ToDictionary(x => x.Id);
		var bank = (await queries.ToListAsync((await bankDetails.GetQueryableAsync()).Where(x => ids.Contains(x.EmployeeId))))
			.ToDictionary(x => x.EmployeeId);
		var (year, monthNumber, _) = PayrollRunRules.ParseMonth(month);
		var firstDay = new DateOnly(year, monthNumber, 1);
		var lastDay = PayrollRunRules.GetPayrollMonthEndDate(year, monthNumber);
		var alreadyIssued = await queries.AnyAsync((await slips.GetQueryableAsync()).Where(x =>
			x.MonthYear == month && ids.Contains(x.EmployeeId) &&
			(x.Status == SalarySlipStatus.Finalized || x.Status == SalarySlipStatus.Paid)));
		if (alreadyIssued || saved.Count != ids.Count || marks.Count != ids.Count || monthly.Count != ids.Count)
			return null;

		var result = new List<PayrollVerifiedRow>(ids.Count);
		foreach (var row in saved.OrderBy(x => x.EmployeeId))
		{
			var item = JsonSerializer.Deserialize<RunPayrollItem>(row.ItemJson)!;
			var snapshot = monthly[row.EmployeeId];
			var review = marks[row.EmployeeId];
			if (item.IsLocked || snapshot.IsLocked || snapshot.ReviewStatus != ReviewStatuses.Reviewed ||
				review.ReviewedAt != snapshot.ReviewedAt || review.ReviewedByUserId != snapshot.ReviewedByUserId ||
				!staff.TryGetValue(row.EmployeeId, out var employee) ||
				!EmployeeLifecycleEligibility.IsEligibleForPayrollMonth(employee, firstDay, lastDay) ||
				PayrollLockPreconditionGuard.HasBlockingLockIssue(snapshot.IssueCodesJson) ||
				!MatchesSnapshot(item, snapshot))
				return null;

			var account = bank.GetValueOrDefault(row.EmployeeId);
			var digest = Fingerprint(new
			{
				tenantId, month, runId, revision, row.Id, row.EmployeeId, row.ItemJson,
				Snapshot = PayrollRunProjection.ToItem(snapshot),
				Review = new { review.Id, review.ReviewedByUserId, review.ReviewedAt },
				Identity = new
				{
					employee.EmployeeName, employee.EmployeeCode, employee.Department, employee.Designation, employee.DateOfJoining,
					account?.BankName, MaskedAccount = SalarySlipProjectionFormatting.MaskAccountNumber(account?.AccountNumber), account?.IfscCode
				}
			});
			result.Add(new(row, item, snapshot, digest));
		}
		return result;
	}

	internal static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
	internal string Seal(PayrollVerifiedRow row) => protector.Protect(row.Digest);

	internal bool Matches(PayrollSubmissionEmployee stored, PayrollVerifiedRow current)
	{
		if (stored.PreviewEmployeeId != current.Preview.Id || stored.NetPay != current.Snapshot.NetPay) return false;
		try { return protector.Unprotect(stored.EvidenceDigest) == current.Digest; }
		catch (CryptographicException) { return false; }
	}

	private static bool MatchesSnapshot(RunPayrollItem item, PayrollRunItemSnapshot snapshot)
	{
		var current = PayrollRunProjection.ToItem(snapshot);
		// JSONB changes whitespace and ordering. Compare the typed row components.
		return !PayrollRunProjection.SnapshotValuesChanged(snapshot, item,
				snapshot.IssueCodesJson, snapshot.AttendancePolicyWarningsJson,
				snapshot.EarningComponentsJson, snapshot.DeductionComponentsJson,
				snapshot.EmployerContributionsJson, snapshot.PayrollAdjustmentsJson)
			&& item.EmployeeId == snapshot.EmployeeId
			&& item.PayrollAttendanceSource == snapshot.PayrollAttendanceSource
			&& item.HolidayImpactJson == snapshot.HolidayImpactJson
			&& item.UsedDefaultDayCalculation == snapshot.UsedDefaultDayCalculation
			&& item.IssueCodes.SequenceEqual(current.IssueCodes)
			&& item.AttendancePolicyWarnings.SequenceEqual(current.AttendancePolicyWarnings)
			&& Fingerprint(item.EarningComponents) == Fingerprint(current.EarningComponents)
			&& Fingerprint(item.DeductionComponents) == Fingerprint(current.DeductionComponents)
			&& Fingerprint(item.EmployerContributions) == Fingerprint(current.EmployerContributions)
			&& Fingerprint(item.PayrollAdjustments) == Fingerprint(current.PayrollAdjustments);
	}
}
