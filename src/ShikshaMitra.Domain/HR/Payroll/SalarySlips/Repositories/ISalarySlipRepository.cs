using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// Repository interface for SalarySlip entity
/// </summary>
public interface ISalarySlipRepository : IRepository<SalarySlip, Guid>
{
	/// <summary>
	/// Gets a salary slip by employee ID and month-year
	/// </summary>
	/// <param name="employeeId">Employee identifier</param>
	/// <param name="monthYear">Month and year in YYYY-MM format</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Salary slip if found, null otherwise</returns>
	Task<SalarySlip?> GetByEmployeeAndMonthAsync(
		Guid employeeId,
		string monthYear,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Checks if a salary slip exists for an employee in a specific month
	/// </summary>
	/// <param name="employeeId">Employee identifier</param>
	/// <param name="monthYear">Month and year in YYYY-MM format</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>True if exists, false otherwise</returns>
	Task<bool> ExistsAsync(
		Guid employeeId,
		string monthYear,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Checks if any finalized salary slip exists for a specific month
	/// </summary>
	/// <param name="monthYear">Month and year in YYYY-MM format</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>True if any finalized slip exists, false otherwise</returns>
	Task<bool> HasFinalizedSlipsForMonthAsync(
		string monthYear,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets all salary slips for a specific month
	/// </summary>
	/// <param name="monthYear">Month and year in YYYY-MM format</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>List of salary slips for the month</returns>
	Task<List<SalarySlip>> GetByMonthAsync(
		string monthYear,
		CancellationToken cancellationToken = default);
}
