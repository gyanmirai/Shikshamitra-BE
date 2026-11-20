using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class AcademicNotificationStaffAccess(IRepository<IdentityUser, Guid> users, AdminPermissionAccess identities,
	IPermissionChecker permissions, ClassTeacherNotificationAccess scopeAccess, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<IReadOnlyList<NotificationRecipientRefDto>> FilterAsync(Guid school, NotificationSourceScopeDto scope,
		IReadOnlyList<NotificationRecipientRefDto> candidates, string viewPermission, CancellationToken token)
	{
		var allowed = new HashSet<Guid>();
		foreach (var actor in candidates.Select(x => x.UserId!.Value).Distinct())
		{
			if (await CanReadAsync(school, actor, scope, viewPermission, token)) allowed.Add(actor);
		}
		return candidates.Where(x => allowed.Contains(x.UserId!.Value)).ToArray();
	}

	private async Task<bool> CanReadAsync(Guid school, Guid actor, NotificationSourceScopeDto scope, string viewPermission,
		CancellationToken token)
	{
		var now = DateTimeOffset.UtcNow;
		var account = await queries.SingleOrDefaultAsync((await users.GetQueryableAsync()).Where(x =>
			x.Id == actor && x.TenantId == school && !x.IsDeleted && x.IsActive)
			.Select(x => new { x.LockoutEnabled, x.LockoutEnd }), token);
		if (account == null || account.LockoutEnabled && account.LockoutEnd > now) return false;
		var roles = await identities.GetRolesAsync(actor);
		if (roles == null) return false;
		var claims = new[] { new Claim(AbpClaimTypes.UserId, actor.ToString()), new Claim(AbpClaimTypes.TenantId, school.ToString()) }
			.Concat(roles.Select(role => new Claim(AbpClaimTypes.Role, role)));
		var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "AcademicNotificationStaff", AbpClaimTypes.UserName, AbpClaimTypes.Role));
		if (!await permissions.IsGrantedAsync(principal, viewPermission)) return false;
		try { await scopeAccess.EnsureScopeAsync(school, actor, scope, token); }
		catch (AbpAuthorizationException) { return false; }
		return true;
	}
}
