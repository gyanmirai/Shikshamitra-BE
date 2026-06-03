using System;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Manager for employee cache operations.
/// </summary>
public interface IEmployeeCacheManager
{
	Task<EmployeeCacheItem?> GetOrAddAsync(
		Guid employeeId,
		Func<Task<Employee?>> factory,
		CancellationToken cancellationToken = default);

	Task<EmployeeCacheItem?> GetByUserIdOrAddAsync(
		Guid userId,
		Func<Task<Employee?>> factory,
		CancellationToken cancellationToken = default);

	Task InvalidateEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

	Task InvalidateByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

	Task InvalidateByCodeAsync(string employeeCode, CancellationToken cancellationToken = default);

	Task ClearAllAsync(CancellationToken cancellationToken = default);
}
