using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.Employees.AcademicPermissions;

[RemoteService(false)]
[Authorize]
public class EmployeeAcademicPermissionAppService(
	IEmployeeRepository employees,
	IEmployeeAuthorizationService employeeAuthorization,
	IIdentityUserRepository users,
	IUserScopeAssignmentRepository assignments,
	IUserScopeService scopes,
	IPermissionChecker permissions,
	UserScopeAppService scopeWriter) : ApplicationService, IEmployeeAcademicPermissionAppService
{
	internal const string DraftAssignmentDescription = "Staff creation academic permission";

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	public async Task<UserScopeHierarchyDto> GetAsync(Guid employeeId, Guid academicYearId)
	{
		await employeeAuthorization.CheckEmployeeAccessAsync(employeeId);
		var employee = await employees.GetAsync(employeeId);
		EnsureTarget(employee);
		return await scopeWriter.ReadEmployeeScopeHierarchyAsync(employee.UserId!.Value, academicYearId);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit)]
	public async Task<BulkScopeAssignmentResultDto> UpdateAsync(Guid employeeId, BulkScopeAssignmentDto input)
	{
		await employeeAuthorization.CheckEmployeeModifyAccessAsync(employeeId);
		var employee = await employees.GetAsync(employeeId);
		EnsureTarget(employee);
		if (input.UserIds == null || input.UserIds.Count != 1 || input.UserIds[0] != employee.UserId)
			throw new AbpAuthorizationException("Academic permissions must target this staff member's linked account.");

		if (!await permissions.IsGrantedAsync(ShikshaMitraPermissions.UserScope.BulkAssign))
		{
			if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add) ||
				employee.CreatorId != CurrentUser.Id || employee.EmployeeStatusType != EmployeeStatusType.Inactive ||
				employee.StatusLastChangedAt.HasValue || employee.UserId == CurrentUser.Id ||
				await AsyncExecuter.AnyAsync((await employees.GetQueryableAsync())
					.Where(x => x.UserId == employee.UserId && x.Id != employeeId)))
				throw new AbpAuthorizationException("Scope management permission is required for an existing staff account.");

			var user = await users.GetAsync(employee.UserId!.Value);
			if (user.TenantId != CurrentTenant.Id || user.CreatorId != CurrentUser.Id)
				throw new AbpAuthorizationException("Scope management permission is required for an existing identity account.");
			var existing = await AsyncExecuter.ToListAsync((await assignments.GetQueryableAsync())
				.Where(x => x.UserId == employee.UserId));
			if (existing.Any(x => x.CreatorId != CurrentUser.Id || x.Description != DraftAssignmentDescription))
				throw new AbpAuthorizationException("Existing academic assignments require scope management permission.");

			var callerScope = await scopes.GetUserScopeAsync(CurrentUser.Id!.Value);
			if (input.Scopes == null || input.Scopes.Any(row => row == null ||
				!ContainsAssignment(callerScope, input.AcademicYearId, row)))
				throw new AbpAuthorizationException("Select academic permissions within your assigned academic access.");
		}

		input.Description = DraftAssignmentDescription;
		return await scopeWriter.ReplaceEmployeeScopesAsync(input);
	}

	private void EnsureTarget(Employee employee)
	{
		if (!CurrentUser.Id.HasValue || employee.TenantId != CurrentTenant.Id || !employee.UserId.HasValue)
			throw new AbpAuthorizationException("A staff account in the current school is required.");
	}

	private static bool ContainsAssignment(UserScopeContext scope, Guid? yearId, BulkScopeAssignmentScopeDto row)
	{
		if (!yearId.HasValue) return false;
		if (scope.HasAcademicYearWideScope(yearId)) return true;
		// Ancestor IDs also occur in narrow scopes; only a wildcard grants its whole subtree.
		return (row.EducationBoardId.HasValue && scope.WildcardMediumBoardIds.Contains(row.EducationBoardId.Value)) ||
			(row.EducationMediumId.HasValue && scope.WildcardMediumStageMediumIds.Contains(row.EducationMediumId.Value)) ||
			(row.EducationMediumStageId.HasValue && scope.WildcardStandardMediumStageIds.Contains(row.EducationMediumStageId.Value)) ||
			(row.StandardId.HasValue && scope.WildcardDivisionStandardIds.Contains(row.StandardId.Value)) ||
			(row.DivisionId.HasValue && scope.DivisionIds.Contains(row.DivisionId.Value));
	}
}
