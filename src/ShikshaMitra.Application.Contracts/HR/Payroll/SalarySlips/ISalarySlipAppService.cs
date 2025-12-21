using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// Application service interface for salary slip operations
/// </summary>
public interface ISalarySlipAppService : IApplicationService
{
	/// <summary>
	/// Fetches salary slip details for a given employee and month-year
	/// Generates the slip if it doesn't exist
	/// </summary>
	/// <param name="employeeId">Employee identifier</param>
	/// <param name="monthYear">Month and year in YYYY-MM format</param>
	/// <returns>Salary slip details with earnings and deductions breakdown</returns>
	Task<SalarySlipDto> GetSalarySlipAsync(Guid employeeId, string monthYear);
}
