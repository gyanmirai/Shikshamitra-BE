using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Permission = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollApprovalAccess(IRepository<PayrollApprovalSettings, Guid> settings,
	IRepository<IdentityUser, Guid> users,
	PayrollScopedEmployeeResolver staff, IUserScopeService scopes, HrReportingAccess principals,
	IPermissionChecker permissions, IAsyncQueryableExecuter queries, ICurrentTenant tenant,
	ICurrentUser currentUser, PayrollSectionScope sections) : ITransientDependency
{
	internal Guid TenantId => tenant.Id ?? throw new AbpAuthorizationException();
	internal Guid ActorId => currentUser.Id ?? throw new AbpAuthorizationException();

	internal async Task<PayrollApprovalSettings?> ReadSettingsAsync() =>
		await settings.FindAsync(x => x.TenantId == TenantId);

	internal async Task EnsureLegacyIssuanceAsync()
	{
		if ((await ReadSettingsAsync())?.Mode is PayrollApprovalSettings.Active or PayrollApprovalSettings.Paused or PayrollApprovalSettings.SectionReview)
			throw new BusinessException("PAYROLL_SUBMISSION_REQUIRED");
	}

	internal IReadOnlyList<PayrollSectionRouteDto> SectionRoutes(PayrollApprovalSettings? setting) =>
		setting?.SectionRoutesJson is { } json ? JsonSerializer.Deserialize<PayrollSectionRouteDto[]>(json) ?? [] : [];

	internal Task<Guid[]> GetCurrentRoleIdsAsync(Guid userId) => sections.GetRoleIdsAsync(userId);

	internal Task<Dictionary<Guid, Guid>> ResolveSectionRoutesAsync(IReadOnlyCollection<Guid> employeeIds,
		Guid? academicYearId, IReadOnlyList<PayrollSectionRouteDto> routes) =>
		sections.ResolveRoutesAsync(employeeIds, academicYearId, routes);

	internal Task<(string? StageName, string? RoleName)> GetSectionRouteNamesAsync(Guid stageId, Guid roleId) =>
		sections.GetRouteNamesAsync(stageId, roleId);

	internal Task<string?> GetSectionReviewerNameAsync(Guid userId) => sections.GetUserNameAsync(userId);

	internal async Task<bool> CoversSectionAsync(Guid userId, Guid stageId, Guid? academicYearId,
		IReadOnlyCollection<Guid> employeeIds)
	{
		var stage = await sections.FindStageAsync(stageId);
		return stage != null && stage.AcademicYearId == academicYearId &&
			await sections.CoversAsync(userId, stage, employeeIds);
	}

	internal async Task<bool> CanSectionReviewAsync(Guid userId, Guid stageId, Guid roleId, Guid? academicYearId,
		IReadOnlyCollection<Guid> employeeIds)
	{
		if (!academicYearId.HasValue || employeeIds.Count == 0) return false;
		var stage = await sections.FindStageAsync(stageId);
		if (stage == null || stage.AcademicYearId != academicYearId) return false;
		return await queries.AnyAsync((await sections.GetRoleMembersAsync(roleId)).Where(user => user.Id == userId)) &&
			await CanReviewSectionAsync(userId, stage, employeeIds);
	}

	internal async Task<bool> HasSectionReviewerAsync(Guid stageId, Guid roleId, Guid? academicYearId,
		IReadOnlyCollection<Guid> employeeIds)
	{
		if (!academicYearId.HasValue || employeeIds.Count == 0) return false;
		var stage = await sections.FindStageAsync(stageId);
		if (stage == null || stage.AcademicYearId != academicYearId) return false;
		var submitter = ActorId;
		var ids = await queries.ToListAsync((await sections.GetRoleMembersAsync(roleId))
			.Where(user => user.Id != submitter).Select(user => user.Id));
		foreach (var id in ids)
			if (await CanReviewSectionAsync(id, stage, employeeIds)) return true;
		return false;
	}

	internal async Task<List<PayrollApproverDto>> GetSectionReviewersAsync(Guid stageId, Guid roleId)
	{
		if (!await CanManageAsync()) throw new AbpAuthorizationException();
		var stage = await sections.FindStageAsync(stageId) ?? throw new BusinessException("PAYROLL_SECTION_ROUTE_INVALID");
		var members = await queries.ToListAsync((await sections.GetRoleMembersAsync(roleId))
			.OrderBy(user => user.UserName).ThenBy(user => user.Id)
			.Select(user => new { user.Id, user.Name, user.Surname, user.UserName }));
		var result = new List<PayrollApproverDto>();
		foreach (var member in members)
		{
			if (!await CanReviewSectionAsync(member.Id, stage, [])) continue;
			var name = $"{member.Name} {member.Surname}".Trim();
			result.Add(new(member.Id, name.Length > 0 ? $"{name} ({member.UserName})" : member.UserName));
		}
		return result;
	}

	private async Task<bool> CanReviewSectionAsync(Guid userId, HrMediumStageScope stage, IReadOnlyCollection<Guid> employeeIds) =>
		await CanAsync(userId, Permission.View, Permission.Add) && await sections.CoversAsync(userId, stage, employeeIds);

	internal async Task<bool> CanManageAsync()
	{
		if (!await CanAsync(ActorId, Permission.ManageWorkflow)) return false;
		var scope = await scopes.GetUserScopeAsync(ActorId);
		return scope.TenantId == TenantId && HrUserScopeAccess.HasHrWideAccess(scope);
	}

	internal async Task<bool> CanAsync(Guid userId, params string[] grants)
	{
		try
		{
			var principal = await principals.PrincipalAsync(userId);
			foreach (var grant in grants)
				if (!await permissions.IsGrantedAsync(principal, grant)) return false;
			return true;
		}
		catch (AbpAuthorizationException)
		{
			return false;
		}
	}

	internal async Task<bool> CoversAsync(Guid userId, IReadOnlyCollection<Guid> ids)
	{
		if (ids.Count == 0) return false;
		try
		{
			var visible = await staff.GetEmployeeQueryForUserAsync(userId);
			return await queries.CountAsync(visible.Where(x => ids.Contains(x.Id))) == ids.Count;
		}
		catch (AbpAuthorizationException)
		{
			return false;
		}
	}

	internal Task<bool> CanApproveAsync(Guid userId, bool selfFinalize) =>
		selfFinalize
			? CanAsync(userId, Permission.View, Permission.Add, Permission.Submit, Permission.Lock)
			: CanAsync(userId, Permission.View, Permission.Lock);

	internal async Task<List<PayrollApproverDto>> GetApproversAsync(string? search)
	{
		if (!await CanManageAsync()) throw new AbpAuthorizationException();
		var term = search?.Trim().ToLowerInvariant();
		var query = (await users.GetQueryableAsync()).Where(x => x.TenantId == TenantId && x.IsActive);
		if (!string.IsNullOrEmpty(term))
			query = query.Where(x => x.UserName.ToLower().Contains(term) ||
				(x.Name != null && x.Name.ToLower().Contains(term)) || (x.Surname != null && x.Surname.ToLower().Contains(term)));
		var matches = new List<PayrollApproverDto>();
		foreach (var user in await queries.ToListAsync(query.OrderBy(x => x.UserName).ThenBy(x => x.Id).Take(100)))
		{
			if (!await CanApproveAsync(user.Id, false)) continue;
			var name = $"{user.Name} {user.Surname}".Trim();
			matches.Add(new(user.Id, name.Length > 0 ? $"{name} ({user.UserName})" : user.UserName));
			if (matches.Count == 25) break;
		}
		return matches;
	}

	internal static bool MatchesRoute(PayrollSubmission batch, PayrollApprovalSettings? setting) =>
		setting != null && (batch.SelfFinalization
			? setting.AllowSelfFinalization && batch.ApproverId == batch.SubmitterId
			: setting.ApproverId == batch.ApproverId && batch.ApproverId != batch.SubmitterId);
}
