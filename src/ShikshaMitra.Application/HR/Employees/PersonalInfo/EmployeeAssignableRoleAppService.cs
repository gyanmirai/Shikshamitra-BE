using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

[RemoteService(false)]
[Authorize]
public class EmployeeAssignableRoleAppService(
	IIdentityRoleRepository roleRepository,
	IPermissionChecker permissionChecker) : ApplicationService, IEmployeeAssignableRoleAppService
{
	public async Task<ListResultDto<EmployeeAssignableRoleDto>> GetListAsync()
	{
		if (!await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add) &&
			!await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit))
		{
			throw new AbpAuthorizationException();
		}

		var roles = await roleRepository.GetListAsync();
		var items = roles
			.Where(role => role.TenantId == CurrentTenant.Id &&
				!string.IsNullOrWhiteSpace(role.Name) &&
				!EmployeeOnboardingRoleAssignment.IsReservedSystemRole(role.Name) &&
				!string.Equals(role.Name.Trim(), EmployeeRoleAudience.Student, StringComparison.OrdinalIgnoreCase) &&
				!string.Equals(role.Name.Trim(), EmployeeRoleAudience.Parent, StringComparison.OrdinalIgnoreCase) &&
				string.Equals(role.GetProperty<string>(EmployeeRoleAudience.ExtraPropertyName),
					EmployeeRoleAudience.Employee, StringComparison.Ordinal) &&
				role.GetProperty<bool>(EmployeeOnboardingRoleAssignment.ExtraPropertyName))
			.OrderBy(role => role.Name)
			.ThenBy(role => role.Id)
			.Select(role => new EmployeeAssignableRoleDto
			{
				Id = role.Id,
				Name = role.Name,
				EmployeeGroupAudience = EmployeeGroupAudience.ResolveOrDefault(
					role.GetProperty<string>(EmployeeGroupAudience.ExtraPropertyName))
			}).ToList();

		return new ListResultDto<EmployeeAssignableRoleDto>(items);
	}
}
