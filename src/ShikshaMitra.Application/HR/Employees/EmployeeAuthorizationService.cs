using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Users;

namespace ShikshaMitra.HR.Employees;

[RemoteService(false)]
public class EmployeeAuthorizationService(
	IEmployeeRepository employeeRepository,
	ICurrentUser currentUser)
	: ApplicationService, IEmployeeAuthorizationService
{
	private const string AdminRole = "admin";
	private const string AdministratorRole = "Administrator";
	private const string HrManagerRole = "HR Manager";
	private const string HrAdminRole = "HR Admin";

	public async Task CheckEmployeeAccessAsync(Guid employeeId, bool requireOwnership = false)
	{
		if (await IsAdminAsync())
		{
			return;
		}

		var employee = await employeeRepository.GetAsync(employeeId);
		EnsureSameTenant(employee, "Access denied: Cross-tenant access not allowed.");

		if (requireOwnership)
		{
			if (employee.UserId != currentUser.Id)
			{
				throw new AbpAuthorizationException("Access denied: You can only access your own data.");
			}

			return;
		}

		var hasHRAccess = await HasHrAccessAsync();
		var isOwnData = employee.UserId == currentUser.Id;

		if (!hasHRAccess && !isOwnData)
		{
			throw new AbpAuthorizationException("Access denied: Insufficient permissions to access employee data.");
		}
	}

	public async Task CheckEmployeeModifyAccessAsync(Guid employeeId)
	{
		if (await IsAdminAsync())
		{
			return;
		}

		var employee = await employeeRepository.GetAsync(employeeId);
		EnsureSameTenant(employee, "Access denied: Cross-tenant modification not allowed.");

		if (!await HasHrAccessAsync())
		{
			throw new AbpAuthorizationException("Access denied: Only HR personnel can modify employee data.");
		}
	}

	public async Task CheckSensitiveFieldAccessAsync(Guid employeeId)
	{
		if (!await IsAdminAsync() && !await IsInRoleAsync(HrAdminRole))
		{
			throw new AbpAuthorizationException("Access denied: Only administrators can modify sensitive employee fields.");
		}

		var employee = await employeeRepository.GetAsync(employeeId);
		EnsureSameTenant(employee, "Access denied: Cross-tenant access not allowed.");
	}

	public async Task CheckBulkOperationAccessAsync()
	{
		if (!await IsAdminAsync() && !await IsInRoleAsync(HrAdminRole))
		{
			throw new AbpAuthorizationException("Access denied: Bulk operations require administrator privileges.");
		}
	}

	private void EnsureSameTenant(Employee employee, string message)
	{
		// Employee lookups are ABP-filtered, but this explicit check protects reused IDOR paths.
		if (employee.TenantId != currentUser.TenantId)
		{
			throw new AbpAuthorizationException(message);
		}
	}

	private async Task<bool> HasHrAccessAsync()
	{
		return await IsInRoleAsync(HrManagerRole) || await IsInRoleAsync(HrAdminRole);
	}

	private async Task<bool> IsAdminAsync()
	{
		return await IsInRoleAsync(AdminRole) || await IsInRoleAsync(AdministratorRole);
	}

	private Task<bool> IsInRoleAsync(string roleName)
	{
		return Task.FromResult(currentUser.IsInRole(roleName));
	}
}
