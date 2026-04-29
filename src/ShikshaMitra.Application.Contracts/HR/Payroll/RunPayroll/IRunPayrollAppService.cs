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
	/// <param name="input">Input with month, page, and pageSize</param>
	/// <returns>Payroll preview with employee items</returns>
	Task<RunPayrollResponse> GetPreviewAsync(GetRunPayrollInput input);

	/// <summary>
	/// Generate and lock payroll for a month
	/// </summary>
	/// <param name="request">Request with month</param>
	/// <returns>Lock confirmation response</returns>
	Task<PayrollLockResponse> LockPayrollAsync(RunPayrollRequest request);

	/// <summary>
	/// Marks selected employee payroll rows as reviewed for the month.
	/// </summary>
	Task<PayrollLockResponse> MarkReviewedAsync(PayrollRunReviewRequest request);

	/// <summary>
	/// Removes review markers for selected employee payroll rows before lock.
	/// </summary>
	Task<PayrollLockResponse> UnmarkReviewedAsync(PayrollRunReviewRequest request);
}

