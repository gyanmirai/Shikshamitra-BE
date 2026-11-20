using System;
using System.Collections.Generic;
using System.Linq;
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
	ICurrentUser currentUser) : ITransientDependency
{
	internal Guid TenantId => tenant.Id ?? throw new AbpAuthorizationException();
	internal Guid ActorId => currentUser.Id ?? throw new AbpAuthorizationException();

	internal async Task<PayrollApprovalSettings?> ReadSettingsAsync() =>
		await settings.FindAsync(x => x.TenantId == TenantId);

	internal async Task EnsureLegacyIssuanceAsync()
	{
		if ((await ReadSettingsAsync())?.Mode is PayrollApprovalSettings.Active or PayrollApprovalSettings.Paused)
			throw new BusinessException("PAYROLL_SUBMISSION_REQUIRED");
	}

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
