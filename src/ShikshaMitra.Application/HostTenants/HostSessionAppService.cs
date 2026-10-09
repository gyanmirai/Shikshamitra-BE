using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.TenantManagement;
using ShikshaMitra.TenantSettings.InitialSetup;

namespace ShikshaMitra.HostTenants;

[Authorize, RemoteService(false)]
public class HostSessionAppService(HostIdentityGuard guard, InitialSetupStatusReader setup) : ShikshaMitraAppService, IHostSessionAppService
{
	public async Task<HostSessionContextDto> GetAsync()
	{
		if (!CurrentUser.IsAuthenticated || CurrentUser.Id == null) throw new AbpAuthorizationException();
		if (CurrentUser.TenantId.HasValue)
		{
			if (CurrentTenant.Id != CurrentUser.TenantId) throw new AbpAuthorizationException();
			var progress = await setup.ReadAsync();
			return new HostSessionContextDto { AccountMode = "tenant", TenantId = CurrentUser.TenantId,
				SetupCompleted = progress.CompletedAt.HasValue, CanManageSetup = await setup.CanManageAsync() };
		}
		await guard.CheckAsync();
		return new HostSessionContextDto
		{
			AccountMode = "host",
			CanViewTenants = await AuthorizationService.IsGrantedAsync(TenantManagementPermissions.Tenants.Default),
			CanCreateTenants = await AuthorizationService.IsGrantedAsync(TenantManagementPermissions.Tenants.Create),
			CanUpdateTenants = await AuthorizationService.IsGrantedAsync(TenantManagementPermissions.Tenants.Update),
			CanManageTenantStatus = await AuthorizationService.IsGrantedAsync(HostTenantConsts.ManageStatusPermission)
		};
	}
}
