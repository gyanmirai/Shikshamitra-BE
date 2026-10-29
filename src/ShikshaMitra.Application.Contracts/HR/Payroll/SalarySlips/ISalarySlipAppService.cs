using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// Application service interface for salary slip operations
/// </summary>
public interface ISalarySlipAppService : IApplicationService
{
	Task<SalarySlipAvailabilityDto> GetAvailabilityAsync(Guid employeeId, string monthYear, Guid? academicYearId = null);

	/// <summary>
	/// Fetches salary slip details for a given employee and month-year
	/// Payroll must already be finalized with an issued document. This read never generates payroll.
	/// </summary>
	/// <param name="employeeId">Employee identifier</param>
	/// <param name="monthYear">Month and year in YYYY-MM format</param>
	/// <returns>Salary slip details with earnings and deductions breakdown</returns>
	Task<SalarySlipDto> GetSalarySlipAsync(Guid employeeId, string monthYear);

	/// <summary>
	/// Generates the official salary slip PDF for a given employee and month-year.
	/// </summary>
	Task<byte[]> GenerateSalarySlipPdfAsync(Guid employeeId, string monthYear);

	/// <summary>
	/// Compatibility PDF route returning only the immutable issued document.
	/// </summary>
	Task<byte[]> GenerateSalarySlipPreviewPdfAsync(Guid employeeId, string monthYear);

	/// <summary>
	/// Compatibility command returning an existing issued salary slip; creation belongs to payroll locking.
	/// </summary>
	/// <param name="input">Generate salary slip input parameters</param>
	/// <returns>Generated salary slip details</returns>
	Task<SalarySlipDto> GenerateSalarySlipAsync(GenerateSalarySlipInput input);

	/// <summary>
	/// Gets a salary slip by its identifier
	/// </summary>
	/// <param name="id">Salary slip identifier</param>
	/// <returns>Salary slip details</returns>
	Task<SalarySlipDto> GetAsync(Guid id);

	/// <summary>
	/// Updates remarks for a draft salary slip
	/// </summary>
	/// <param name="id">Salary slip identifier</param>
	/// <param name="input">Update input containing new remarks</param>
	/// <returns>Updated salary slip details</returns>
	Task<SalarySlipDto> UpdateAsync(Guid id, UpdateSalarySlipInput input);

	/// <summary>
	/// Finalizes a salary slip, making it immutable
	/// </summary>
	/// <param name="id">Salary slip identifier</param>
	/// <returns>Finalized salary slip details</returns>
	Task<SalarySlipDto> FinalizeAsync(Guid id);

	/// <summary>
	/// Deletes a draft salary slip
	/// </summary>
	/// <param name="id">Salary slip identifier</param>
	Task DeleteAsync(Guid id);
}
