using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

/// <summary>
/// Application service interface for running payroll
/// </summary>
public interface IRunPayrollAppService : IApplicationService
{
	/// <summary>
	/// Get payroll preview for a specific month (paginated)
	/// </summary>
	Task<RunPayrollResponse> GetPreviewAsync(GetRunPayrollInput input);

	/// <summary>
	/// Get lightweight payroll readiness for a month without computing or saving preview snapshots.
	/// </summary>
	Task<RunPayrollReadinessSummary> GetReadinessAsync(string month);

	/// <summary>
	/// Generate and lock payroll for a month
	/// </summary>
	Task<PayrollLockResponse> LockPayrollAsync(RunPayrollRequest request);

	/// <summary>
	/// Marks selected employee payroll rows as reviewed for the month.
	/// </summary>
	Task<PayrollLockResponse> MarkReviewedAsync(PayrollRunReviewRequest request);

	/// <summary>
	/// Removes review markers for selected employee payroll rows before lock.
	/// </summary>
	Task<PayrollLockResponse> UnmarkReviewedAsync(PayrollRunReviewRequest request);

	Task<List<PayrollAdjustmentDto>> GetAdjustmentsAsync(string month, Guid? employeeId = null);

	Task<PayrollAdjustmentDto> CreateAdjustmentAsync(PayrollAdjustmentRequest request);

	Task<PayrollAdjustmentDto> UpdateAdjustmentAsync(Guid id, PayrollAdjustmentRequest request);

	Task DeleteAdjustmentAsync(Guid id);
}
