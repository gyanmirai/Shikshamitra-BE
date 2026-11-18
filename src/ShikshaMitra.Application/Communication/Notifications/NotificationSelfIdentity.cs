using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationSelfContext(Guid TenantId, Guid UserId);

[RemoteService(false)]
public sealed class NotificationSelfIdentity(ICurrentTenant tenant, ICurrentUser current,
	IRepository<IdentityUser, Guid> users) : ITransientDependency
{
	public async Task<NotificationSelfContext> RequireAsync(CancellationToken cancellationToken)
	{
		if (!current.IsAuthenticated || tenant.Id is not Guid tenantId || current.Id is not Guid userId || current.TenantId != tenantId)
			throw new AbpAuthorizationException();
		var user = await users.FindAsync(x => x.Id == userId && x.TenantId == tenantId && !x.IsDeleted, cancellationToken: cancellationToken);
		if (user == null || !user.IsActive || user.LockoutEnabled && user.LockoutEnd > DateTimeOffset.UtcNow)
			throw new AbpAuthorizationException();
		return new(tenantId, userId);
	}
}
