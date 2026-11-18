using System;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Academic.Notifications;

internal static class AcademicNotificationIdentityAccess
{
	public static async Task<(Guid TenantId, Guid ActorId, UserScopeContext Scope)> RequireAsync(ICurrentTenant tenant,
		ICurrentUser current, IRepository<IdentityUser, Guid> users, IUserScopeAccessService scopes,
		IPermissionChecker permissions, string permission, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || !current.IsAuthenticated || current.Id is not Guid actorId ||
			current.TenantId != tenantId || !await permissions.IsGrantedAsync(permission)) throw new AbpAuthorizationException();
		var actor = await users.FindAsync(actorId, cancellationToken: cancellationToken);
		if (actor?.TenantId != tenantId || !actor.IsActive || actor.IsDeleted ||
			actor.LockoutEnabled && actor.LockoutEnd > DateTimeOffset.UtcNow) throw new AbpAuthorizationException();
		var scope = await scopes.GetCurrentUserScopeAsync();
		if (scope.TenantId != tenantId || scope.UserId != actorId || !scope.HasAnyAccess) throw new AbpAuthorizationException();
		return (tenantId, actorId, scope);
	}
}
