using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Records;

/// <summary>Records authorization checks the current database identity, including older valid sessions.</summary>
[RemoteService(false)]
public class StudentRecordsActorService(IdentityUserManager users) : ApplicationService, ITransientDependency
{
	public async Task<IdentityUser> EnsureActiveAsync()
	{
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		var user = await users.FindByIdAsync(CurrentUser.Id.Value.ToString());
		if (user == null || user.TenantId != CurrentTenant.Id || !user.IsActive) throw new AbpAuthorizationException();
		return user;
	}
}
