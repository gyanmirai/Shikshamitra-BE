using System;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Users;

namespace ShikshaMitra.HR.Employees;

[RemoteService(false)]
public class EmployeeAuthorizationService(
	IEmployeeRepository employeeRepository,
	IPermissionChecker permissionChecker,
	ICurrentUser currentUser)
	: ApplicationService, IEmployeeAuthorizationService
{
	public async Task CheckEmployeeAccessAsync(Guid employeeId, bool requireOwnership = false)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		EnsureSameTenant(employee, "Access denied: Cross-tenant access not allowed.");

		var canReadEmployeeList = await CanReadEmployeeListAsync();
		var isOwnData = employee.UserId == currentUser.Id;
		if (requireOwnership)
		{
			if (!isOwnData && !canReadEmployeeList)
			{
				throw new AbpAuthorizationException("Access denied: You can only access your own data.");
			}

			return;
		}

		if (!canReadEmployeeList && !isOwnData)
		{
			throw new AbpAuthorizationException("Access denied: Insufficient permissions to access employee data.");
		}
	}

	public async Task CheckEmployeeModifyAccessAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		EnsureSameTenant(employee, "Access denied: Cross-tenant modification not allowed.");

		if (!await CanManageSensitiveFieldsAsync())
		{
			throw new AbpAuthorizationException("Access denied: Only HR personnel can modify employee data.");
		}
	}

	public async Task CheckSensitiveFieldAccessAsync(Guid employeeId)
	{
		if (!await CanManageBulkOperationsAsync())
		{
			throw new AbpAuthorizationException("Access denied: Only administrators can modify sensitive employee fields.");
		}

		var employee = await employeeRepository.GetAsync(employeeId);
		EnsureSameTenant(employee, "Access denied: Cross-tenant access not allowed.");
	}

	public async Task CheckBulkOperationAccessAsync()
	{
		if (!await CanModifyEmployeeListAsync())
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

	private async Task<bool> CanReadEmployeeListAsync() =>
		await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View) ||
		await CanModifyEmployeeListAsync();

	private async Task<bool> CanModifyEmployeeListAsync() =>
		await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit);

	private async Task<bool> CanManageSensitiveFieldsAsync() =>
		await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.SensitiveFields) ||
		await CanManageEmployeePayrollAsync();

	private async Task<bool> CanManageBulkOperationsAsync() =>
		await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.BulkOperations);

	private Task<bool> CanManageEmployeePayrollAsync() =>
		permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_PayrollStructure.Add);
}
