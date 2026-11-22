using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollSectionScope(
	IRepository<EducationMediumStage, Guid> stages,
	IRepository<IdentityRole, Guid> roles,
	IRepository<IdentityUser, Guid> users,
	IUserScopeService scopes,
	HrEmployeeScopeQueryService employees,
	HrUserScopeAccessService stageAccess,
	HrMediumStageScopeReader stageScopes,
	IAsyncQueryableExecuter queries,
	ICurrentTenant tenant) : ITransientDependency
{
	public async Task<PayrollSectionOptionsDto> GetOptionsAsync()
	{
		var stageRows = await queries.ToListAsync((await stages.GetQueryableAsync())
			.Where(stage => stage.TenantId == tenant.Id &&
				stage.EducationMedium.TenantId == tenant.Id &&
				stage.EducationMedium.EducationBoard.TenantId == tenant.Id &&
				stage.EducationMedium.EducationBoard.AcademicYear.TenantId == tenant.Id)
			.Select(stage => new
			{
				stage.Id,
				stage.Name,
				Medium = stage.EducationMedium.Name,
				Board = stage.EducationMedium.EducationBoard,
				Year = stage.EducationMedium.EducationBoard.AcademicYear.Name
			}));
		var stageOptions = stageRows.Select(row => new PayrollSectionOptionDto(
			row.Id, $"{row.Name} ({row.Medium}, {row.Board.CustomName ?? row.Board.Name}, {row.Year})"))
			.OrderBy(x => x.Name).ThenBy(x => x.Id).ToArray();
		var roleOptions = await queries.ToListAsync((await roles.GetQueryableAsync())
			.Where(role => role.TenantId == tenant.Id)
			.OrderBy(role => role.Name).ThenBy(role => role.Id)
			.Select(role => new PayrollSectionOptionDto(role.Id, role.Name)));
		return new PayrollSectionOptionsDto(stageOptions, roleOptions);
	}

	public async Task<(string? StageName, string? RoleName)> GetRouteNamesAsync(Guid stageId, Guid roleId) =>
		((await stages.FindAsync(stage => stage.Id == stageId && stage.TenantId == tenant.Id))?.Name,
			(await roles.FindAsync(role => role.Id == roleId && role.TenantId == tenant.Id))?.Name);

	public async Task<string?> GetUserNameAsync(Guid userId)
	{
		var user = await users.FindAsync(user => user.Id == userId && user.TenantId == tenant.Id);
		if (user == null) return null;
		var fullName = $"{user.Name} {user.Surname}".Trim();
		return fullName.Length > 0 ? fullName : user.UserName;
	}

	public async Task<Guid[]> GetRoleIdsAsync(Guid userId)
	{
		var matching = (await users.GetQueryableAsync())
			.Where(user => user.Id == userId && user.TenantId == tenant.Id && user.IsActive)
			.SelectMany(user => user.Roles).Select(link => link.RoleId);
		return (await queries.ToListAsync((await roles.GetQueryableAsync())
			.Where(role => role.TenantId == tenant.Id && matching.Contains(role.Id))
			.Select(role => role.Id))).ToArray();
	}

	public async Task<IQueryable<IdentityUser>> GetRoleMembersAsync(Guid roleId)
	{
		var roleIds = (await roles.GetQueryableAsync())
			.Where(role => role.TenantId == tenant.Id && role.Id == roleId).Select(role => role.Id);
		return (await users.GetQueryableAsync())
			.Where(user => user.TenantId == tenant.Id && user.IsActive &&
				user.Roles.Any(link => roleIds.Contains(link.RoleId)));
	}

	public async Task<Dictionary<Guid, Guid>> ResolveRoutesAsync(IReadOnlyCollection<Guid> employeeIds,
		Guid? academicYearId, IReadOnlyList<PayrollSectionRouteDto> routes)
	{
		if (!academicYearId.HasValue) throw new BusinessException("PAYROLL_SECTION_ACADEMIC_YEAR_REQUIRED");
		var routesByStage = routes.ToDictionary(route => route.StageId, route => route.RoleId);
		var employeeStages = await employees.BuildEmployeeStageLookupAsync(employeeIds, academicYearId);
		var result = new Dictionary<Guid, Guid>();
		foreach (var employeeId in employeeIds)
		{
			var matchingStages = employeeStages.GetValueOrDefault(employeeId)?.Where(routesByStage.ContainsKey).ToArray() ?? [];
			if (matchingStages.Length != 1)
				throw new BusinessException(matchingStages.Length == 0
					? "PAYROLL_SECTION_ROUTE_MISSING" : "PAYROLL_SECTION_ROUTE_AMBIGUOUS").WithData("employeeId", employeeId);
			result.Add(employeeId, matchingStages[0]);
		}
		return result;
	}

	public async Task<HrMediumStageScope?> FindStageAsync(Guid stageId) =>
		(await stageScopes.GetByStageIdsAsync([stageId])).GetValueOrDefault(stageId);

	public async Task<bool> CoversAsync(Guid userId, HrMediumStageScope stage, IReadOnlyCollection<Guid> employeeIds)
	{
		var scope = await scopes.GetUserScopeAsync(userId);
		if (scope.TenantId != tenant.Id || !await stageAccess.CanAccessEmployeeStageAsync(scope, stage.StageId)) return false;
		if (employeeIds.Count == 0) return true;
		var employeesInStage = await employees.BuildScopedEmployeeQueryAsync(scope,
			new HrEmployeeScopeOptions { EducationMediumStageId = stage.StageId });
		var ids = employeeIds.Distinct().ToArray();
		return await queries.CountAsync(employeesInStage.Where(employee =>
			employee.TenantId == tenant.Id && Enumerable.Contains(ids, employee.Id))) == ids.Length;
	}
}
