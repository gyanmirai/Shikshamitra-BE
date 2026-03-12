using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Users;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Service to handle employee-specific authorization checks
/// Prevents IDOR (Insecure Direct Object Reference) vulnerabilities
/// </summary>
public class EmployeeAuthorizationService : ApplicationService, IEmployeeAuthorizationService
{
	private readonly IEmployeeRepository _employeeRepository;
	private readonly ICurrentUser _currentUser;

	public EmployeeAuthorizationService(
		IEmployeeRepository employeeRepository,
		ICurrentUser currentUser)
	{
		_employeeRepository = employeeRepository;
		_currentUser = currentUser;
	}

	/// <summary>
	/// Checks if the current user can access a specific employee's data.
	/// Throws AbpAuthorizationException if access is denied.
	/// </summary>
	/// <param name="employeeId">The employee ID to check access for</param>
	/// <param name="requireOwnership">If true, only the employee themselves can access (stricter check)</param>
	/// <exception cref="AbpAuthorizationException">Thrown when access is denied</exception>
	public async Task CheckEmployeeAccessAsync(Guid employeeId, bool requireOwnership = false)
	{
		// System administrators always have access
		if (await IsAdminAsync())
		{
			return;
		}

		var employee = await _employeeRepository.GetAsync(employeeId);

		// Check tenant isolation
		if (employee.TenantId != _currentUser.TenantId)
		{
			throw new AbpAuthorizationException("Access denied: Cross-tenant access not allowed.");
		}

		// For ownership-based access (e.g., viewing own payroll)
		if (requireOwnership)
		{
			if (employee.UserId != _currentUser.Id)
			{
				throw new AbpAuthorizationException("Access denied: You can only access your own data.");
			}
			return;
		}

		// For general access: Allow if user is HR Manager or accessing their own data
		var hasHRAccess = await IsInRoleAsync("HR Manager") || await IsInRoleAsync("HR Admin");
		var isOwnData = employee.UserId == _currentUser.Id;

		if (!hasHRAccess && !isOwnData)
		{
			throw new AbpAuthorizationException("Access denied: Insufficient permissions to access employee data.");
		}
	}

	/// <summary>
	/// Checks if current user can modify employee data
	/// </summary>
	public async Task CheckEmployeeModifyAccessAsync(Guid employeeId)
	{
		// System administrators always have access
		if (await IsAdminAsync())
		{
			return;
		}

		var employee = await _employeeRepository.GetAsync(employeeId);

		// Check tenant isolation
		if (employee.TenantId != _currentUser.TenantId)
		{
			throw new AbpAuthorizationException("Access denied: Cross-tenant modification not allowed.");
		}

		// Only HR Manager/Admin can modify employee data
		var hasHRAccess = await IsInRoleAsync("HR Manager") || await IsInRoleAsync("HR Admin");

		if (!hasHRAccess)
		{
			throw new AbpAuthorizationException("Access denied: Only HR personnel can modify employee data.");
		}
	}

	/// <summary>
	/// Checks if current user can modify sensitive fields (salary, role, etc.)
	/// </summary>
	public async Task CheckSensitiveFieldAccessAsync(Guid employeeId)
	{
		// Only administrators and HR Admin can modify sensitive fields
		if (!await IsAdminAsync() && !await IsInRoleAsync("HR Admin"))
		{
			throw new AbpAuthorizationException("Access denied: Only administrators can modify sensitive employee fields.");
		}

		var employee = await _employeeRepository.GetAsync(employeeId);

		// Verify tenant isolation
		if (employee.TenantId != _currentUser.TenantId)
		{
			throw new AbpAuthorizationException("Access denied: Cross-tenant access not allowed.");
		}
	}

	/// <summary>
	/// Checks if user has bulk operation permissions
	/// </summary>
	public async Task CheckBulkOperationAccessAsync()
	{
		// Only administrators can perform bulk operations
		if (!await IsAdminAsync() && !await IsInRoleAsync("HR Admin"))
		{
			throw new AbpAuthorizationException("Access denied: Bulk operations require administrator privileges.");
		}
	}

	private async Task<bool> IsAdminAsync()
	{
		return await IsInRoleAsync("admin") || await IsInRoleAsync("Administrator");
	}

	private async Task<bool> IsInRoleAsync(string roleName)
	{
		// ABP framework automatically handles role checks with tenant context
		return _currentUser.IsInRole(roleName);
	}
}
