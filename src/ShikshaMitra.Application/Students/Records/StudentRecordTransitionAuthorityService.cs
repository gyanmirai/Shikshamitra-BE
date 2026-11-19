using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.Students.Records;

/// <summary>Every recorded approval must still have current central grants and database roles at commit.</summary>
[RemoteService(false)]
public class StudentRecordTransitionAuthorityService(IdentityUserManager users, ICurrentPrincipalAccessor principals,
	IPermissionChecker permissions, IRepository<StudentRecordTransitionEvent, Guid> events) : ApplicationService, ITransientDependency
{
	public async Task EnsureCurrentPermissionsAsync(params string[] requiredPermissions)
	{
		var actorId = CurrentUser.Id ?? throw new AbpAuthorizationException();
		foreach (var permission in requiredPermissions) await ValidateActorAsync(actorId, permission, null);
	}
	public async Task<bool> HasCurrentPermissionsAsync(params string[] requiredPermissions)
	{
		try { await EnsureCurrentPermissionsAsync(requiredPermissions); return true; }
		catch (AbpAuthorizationException) { return false; }
	}
	public Task EnsureRecordedActorPermissionAsync(Guid actorId, string permission) => ValidateActorAsync(actorId, permission, null);
	public async Task ValidateAsync(StudentRecordTransition item)
	{
		var rule = JsonSerializer.Deserialize<StudentRecordsChangeRule>(item.ApprovalRuleJson)!;
		if (item.ApprovalStageCount == 0) { await ValidateActorAsync(item.RequestedBy, ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create, null); return; }
		var approvals = await events.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.TransitionId == item.Id && x.Action == "Approved");
		for (var stage = 1; stage <= item.ApprovalStageCount; stage++)
		{
			var approval = approvals.SingleOrDefault(x => x.Stage == stage) ?? throw new AbpAuthorizationException();
			if (approval.ActorId == item.RequestedBy) throw new AbpAuthorizationException();
			await ValidateActorAsync(approval.ActorId, ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Approve, rule.Stages[stage - 1].RoleNames);
		}
	}
	private async Task ValidateActorAsync(Guid actorId, string permission, List<string>? stageRoles)
	{
		var user = await users.FindByIdAsync(actorId.ToString());
		if (user == null || user.TenantId != CurrentTenant.Id || !user.IsActive) throw new AbpAuthorizationException();
		var roles = await users.GetRolesAsync(user);
		if (stageRoles != null && !stageRoles.Intersect(roles, StringComparer.OrdinalIgnoreCase).Any()) throw new AbpAuthorizationException();
		var claims = new List<Claim> { new(AbpClaimTypes.UserId, actorId.ToString()), new(AbpClaimTypes.UserName, user.UserName), new(AbpClaimTypes.TenantId, CurrentTenant.Id!.Value.ToString()) };
		claims.AddRange(roles.Select(role => new Claim(AbpClaimTypes.Role, role)));
		using var changed = principals.Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "StudentRecordAuthorityRecheck")));
		if (!await permissions.IsGrantedAsync(permission)) throw new AbpAuthorizationException();
	}
}
