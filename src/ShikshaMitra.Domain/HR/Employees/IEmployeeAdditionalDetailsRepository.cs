using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Repository interface for EmployeeAdditionalDetails entity.
/// </summary>
public interface IEmployeeAdditionalDetailsRepository : IRepository<EmployeeAdditionalDetails, Guid>
{
	/// <summary>
	/// Gets the additional details for a specific employee.
	/// </summary>
	/// <param name="employeeId">The employee ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The employee additional details, or null if not found.</returns>
	Task<EmployeeAdditionalDetails?> GetByEmployeeIdAsync(
		Guid employeeId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Checks if additional details exist for a specific employee.
	/// </summary>
	/// <param name="employeeId">The employee ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if additional details exist, false otherwise.</returns>
	Task<bool> ExistsForEmployeeAsync(
		Guid employeeId,
		CancellationToken cancellationToken = default);
}


