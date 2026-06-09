using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.HR.Employees;

[RemoteService(false)]
public class EmployeeAuthorizationService(
	IEmployeeRepository employeeRepository,
	IPermissionChecker permissionChecker,
	ICurrentUser currentUser,
	ICurrentTenant currentTenant,
	IUserScopeService userScopeService,
	HrUserScopeAccessService hrScopeAccessService,
	HrEmployeeScopeQueryService employeeScopeQueryService)
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

			if (!isOwnData)
			{
				await EnsureEmployeeInCurrentUserScopeAsync(employeeId, nameof(CheckEmployeeAccessAsync));
			}

			return;
		}

		if (!canReadEmployeeList && !isOwnData)
		{
			throw new AbpAuthorizationException("Access denied: Insufficient permissions to access employee data.");
		}

		if (!isOwnData)
		{
			await EnsureEmployeeInCurrentUserScopeAsync(employeeId, nameof(CheckEmployeeAccessAsync));
		}
	}

	public async Task CheckEmployeeModifyAccessAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		EnsureSameTenant(employee, "Access denied: Cross-tenant modification not allowed.");

		if (!await CanModifyEmployeeListAsync())
		{
			throw new AbpAuthorizationException("Access denied: Employee edit permission is required to modify employee data.");
		}

		await EnsureEmployeeInCurrentUserScopeAsync(employeeId, nameof(CheckEmployeeModifyAccessAsync));
	}

	private void EnsureSameTenant(Employee employee, string message)
	{
		// Employee lookups are ABP-filtered, but this explicit check protects reused IDOR paths.
		var currentTenantId = NormalizeTenantId(currentTenant.Id);
		if (currentTenantId.HasValue && NormalizeTenantId(employee.TenantId) != currentTenantId)
		{
			throw new AbpAuthorizationException(message);
		}
	}

	private static Guid? NormalizeTenantId(Guid? tenantId) =>
		tenantId == Guid.Empty ? null : tenantId;

	private async Task<bool> CanReadEmployeeListAsync() =>
		await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View) ||
		await CanModifyEmployeeListAsync();

	private async Task<bool> CanModifyEmployeeListAsync() =>
		await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit);

	private async Task EnsureEmployeeInCurrentUserScopeAsync(Guid employeeId, string operation)
	{
		if (currentUser.IsInRole("admin"))
		{
			return;
		}

		var userId = currentUser.Id;
		if (!userId.HasValue)
		{
			throw new AbpAuthorizationException("Access denied: Authenticated user is required.");
		}

		var scope = await userScopeService.GetUserScopeAsync(userId.Value);
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return;
		}

		if (!HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope))
		{
			hrScopeAccessService.LogDeniedAccess(scope, operation, "Employee", employeeId);
			throw new AbpAuthorizationException("Access denied: Employee is outside your organization scope.");
		}

		var stageLookup = await employeeScopeQueryService.BuildEmployeeStageLookupAsync([employeeId]);
		if (!stageLookup.TryGetValue(employeeId, out var stageIds) || stageIds.Count == 0)
		{
			hrScopeAccessService.LogDeniedAccess(scope, operation, "Employee", employeeId);
			throw new AbpAuthorizationException("Access denied: Employee is outside your organization scope.");
		}

		var accessibleIds = await hrScopeAccessService.FilterEmployeeIdsByStagesAsync(
			[employeeId],
			stageLookup.ToDictionary(x => x.Key, x => (IReadOnlyCollection<Guid>)x.Value),
			scope,
			includeUnknownStageEmployees: false);

		if (accessibleIds.Count == 0)
		{
			hrScopeAccessService.LogDeniedAccess(scope, operation, "Employee", employeeId);
			throw new AbpAuthorizationException("Access denied: Employee is outside your organization scope.");
		}
	}
}
