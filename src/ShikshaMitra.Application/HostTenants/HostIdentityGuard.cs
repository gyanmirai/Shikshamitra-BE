using System.Threading.Tasks;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.HostTenants;

public class HostIdentityGuard(ICurrentUser currentUser, ICurrentTenant currentTenant, IIdentityUserRepository users) : ITransientDependency
{
	public async Task CheckAsync()
	{
		if (!currentUser.IsAuthenticated || currentUser.Id == null || currentUser.TenantId != null || currentTenant.Id != null)
			throw new AbpAuthorizationException();
		var user = await users.FindAsync(currentUser.Id.Value);
		if (user == null || user.TenantId != null || !user.IsActive) throw new AbpAuthorizationException();
	}
}
