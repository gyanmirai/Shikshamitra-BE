using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

[RemoteService(false)]
public class SalarySlipAvailabilityReader(SalarySlipIssuedReader issuedReader,
	ISalarySlipRepository slips, IRepository<PayslipDocument, Guid> documents,
	IRepository<PayrollRunItemSnapshot, Guid> snapshots, IRepository<PayrollPreviewRun, Guid> runs,
	IRepository<PayrollRunReview, Guid> reviews, IPayrollInputRevisionStore revisions,
	PayrollScopedEmployeeResolver scopeResolver, PayrollAcademicYearScopeResolver academicYears)
	: ApplicationService, ITransientDependency
{
	public async Task<SalarySlipAvailabilityDto> GetAsync(Guid employeeId, string monthYear, Guid? academicYearId)
	{
		await issuedReader.EnsureEmployeeAsync(employeeId);
		var month = SalarySlipMonthYearNormalizer.Normalize(monthYear);
		var (year, monthNumber, _) = PayrollRunRules.ParseMonth(month);
		var slip = await AsyncExecuter.FirstOrDefaultAsync((await slips.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && x.EmployeeId == employeeId && x.MonthYear == month)
			.Select(x => new { x.Id, x.Status }));
		if (slip?.Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid)
		{
			var issued = await documents.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.SalarySlipId == slip.Id &&
				x.EmployeeId == employeeId && x.MonthYear == month);
			return Describe(employeeId, month, issued ? "AVAILABLE" : "ISSUANCE_REQUIRED");
		}
		var academicYear = await academicYears.ResolveAsync(academicYearId, year, monthNumber);

		// Only inspect a generation for the caller's exact current employee scope and academic context.
		var scoped = await scopeResolver.GetScopedPayrollEmployeesAsync(year, monthNumber, false);
		if (!scoped.Any(x => x.Id == employeeId)) return Describe(employeeId, month, "UNAVAILABLE");
		var fingerprint = PayrollPreviewRunAppService.ScopeFingerprint(scoped.Select(employee => employee.Id));
		var latest = await AsyncExecuter.FirstOrDefaultAsync((await runs.GetQueryableAsync())
				.Where(x => x.TenantId == CurrentTenant.Id && x.Month == month && x.AcademicYearId == academicYear &&
					x.ScopeFingerprint == fingerprint)
				.OrderByDescending(x => x.CreatedAtUtc)
				.Select(x => new { x.Id, x.Status, x.InputRevision }));
		if (latest?.Status is "QUEUED" or "RUNNING") return Describe(employeeId, month, "PREPARING");
		if (latest?.Status == "FAILED") return Describe(employeeId, month, "FAILED");
		var snapshot = await AsyncExecuter.FirstOrDefaultAsync((await snapshots.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && x.EmployeeId == employeeId && x.MonthYear == month)
			.Select(x => new { x.ReviewStatus }));
		if (snapshot == null) return Describe(employeeId, month, "NOT_PREPARED");
		var current = latest?.Status == "SUCCEEDED" && latest.InputRevision == await revisions.ReadAsync();
		if (current && snapshot.ReviewStatus == ReviewStatuses.Reviewed &&
			await reviews.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.EmployeeId == employeeId &&
				x.MonthYear == month && x.PreviewRunId == latest!.Id))
			return Describe(employeeId, month, "REVIEWED");
		return Describe(employeeId, month, "PENDING");
	}

	internal static SalarySlipAvailabilityDto Describe(Guid employeeId, string month, string status) => new(employeeId, month,
		status, status switch
		{
			"AVAILABLE" => "The issued payslip is available.",
			"UNAVAILABLE" => "No payslip is available for this staff member in the selected month. Review the employment dates and payroll eligibility.",
			"ISSUANCE_REQUIRED" => "Payroll is finalized, but its issued payslip document is unavailable. Contact your payroll administrator.",
			"NOT_PREPARED" => "Payroll has not been prepared or locked for this month. Prepare payroll, review it and lock it before viewing the payslip.",
			"PREPARING" => "Payroll is being prepared. Wait for calculation to finish, then review and lock payroll before viewing the payslip.",
			"FAILED" => "Payroll preparation failed. Retry payroll preparation, then review and lock payroll before viewing the payslip.",
			"REVIEWED" => "Payroll is reviewed but has not been locked. Lock the reviewed payroll before viewing the payslip.",
			_ => "Payroll has not been locked. Refresh the payroll preview, review the current amounts and lock payroll before viewing the payslip."
		}, status == "AVAILABLE");
}
