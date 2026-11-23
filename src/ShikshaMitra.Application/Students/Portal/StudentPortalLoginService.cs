using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Portal;

/// <summary>Portal-only login eligibility; does not change staff authentication rules.</summary>
[RemoteService(false)]
public class StudentPortalLoginService(IRepository<IdentityUser, Guid> identities, StudentAppAccessEnforcement appAccess)
	: ApplicationService, ITransientDependency
{
	public Task<bool> IsPortalUserAsync(IdentityUser user) => appAccess.IsPortalUserAsync(user);

	public async Task<string?> FindMobileUsernameAsync(string? identifier)
	{
		if (!CurrentTenant.Id.HasValue || string.IsNullOrWhiteSpace(identifier)) return null;
		var phone = identifier.Trim();
		if (!phone.All(c => char.IsDigit(c) || c == '+')) return null;
		var matches = await identities.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.PhoneNumber == phone);
		// Shared numbers never choose an arbitrary identity, including a staff identity.
		if (matches.Count != 1) return null;
		var user = matches[0];
		return user.IsActive && !string.IsNullOrWhiteSpace(user.Email) && await IsPortalUserAsync(user) ? user.UserName : null;
	}

	public Task<bool> CanAccessAsync(IdentityUser user) => appAccess.HasAvailableAssociationAsync(user);
}
