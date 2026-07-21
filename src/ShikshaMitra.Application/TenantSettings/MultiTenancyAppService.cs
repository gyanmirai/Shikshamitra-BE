using System;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.TenantSettings;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(ITenantAppService), typeof(MultiTenancyAppService))]
[AllowAnonymous]
public class MultiTenancyAppService : TenantAppService
{
	private readonly IPermissionChecker _permissionChecker;
	private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

	public MultiTenancyAppService(
		ITenantRepository tenantRepository,
		ITenantManager tenantManager,
		IDataSeeder dataSeeder,
		IDistributedEventBus distributedEventBus,
		ILocalEventBus localEventBus,
		IPermissionChecker permissionChecker,
		ICurrentPrincipalAccessor currentPrincipalAccessor
	)
		: base(tenantRepository, tenantManager, dataSeeder, distributedEventBus, localEventBus)
	{
		_permissionChecker = permissionChecker;
		_currentPrincipalAccessor = currentPrincipalAccessor;
	}

	[AllowAnonymous]
	public override async Task<PagedResultDto<TenantDto>> GetListAsync(GetTenantsInput input)
	{
		var tenantId = GetEffectiveTenantId();
		if (tenantId == null)
		{
			await CheckTenantViewPermissionAsync();
			return await base.GetListAsync(input);
		}

		CheckAuthenticatedTenantUser();
		var currentTenant = await base.GetAsync(tenantId.Value);
		return new PagedResultDto<TenantDto>(1, new[] { currentTenant });
	}

	public override async Task<TenantDto> CreateAsync(TenantCreateDto input)
	{
		await CheckHostTenantOperationAsync(TenantManagementPermissions.Tenants.Create);
		return await base.CreateAsync(input);
	}

	public override async Task<TenantDto> UpdateAsync(Guid id, TenantUpdateDto input)
	{
		await CheckTenantUpdatePermissionAsync();

		var tenantId = GetEffectiveTenantId();
		if (tenantId != null && tenantId != id)
		{
			throw new AbpAuthorizationException("You are not allowed to update this tenant.");
		}

		return await base.UpdateAsync(id, input);
	}

	[AllowAnonymous]
	public override async Task<TenantDto> GetAsync(Guid id)
	{
		var tenantId = GetEffectiveTenantId();
		if (tenantId != null && tenantId != id)
		{
			await CheckTenantViewPermissionAsync();
			throw new AbpAuthorizationException("You are not allowed to view this tenant.");
		}

		if (tenantId == id)
		{
			CheckAuthenticatedTenantUser();
		}
		else
		{
			await CheckTenantViewPermissionAsync();
		}

		return await base.GetAsync(id);
	}

	public override async Task DeleteAsync(Guid id)
	{
		await CheckHostTenantOperationAsync(TenantManagementPermissions.Tenants.Delete);
		await base.DeleteAsync(id);
	}

	public override async Task<string> GetDefaultConnectionStringAsync(Guid id)
	{
		await CheckHostTenantOperationAsync(TenantManagementPermissions.Tenants.ManageConnectionStrings);
		return await base.GetDefaultConnectionStringAsync(id);
	}

	public override async Task UpdateDefaultConnectionStringAsync(Guid id, string defaultConnectionString)
	{
		await CheckHostTenantOperationAsync(TenantManagementPermissions.Tenants.ManageConnectionStrings);
		await base.UpdateDefaultConnectionStringAsync(id, defaultConnectionString);
	}

	public override async Task DeleteDefaultConnectionStringAsync(Guid id)
	{
		await CheckHostTenantOperationAsync(TenantManagementPermissions.Tenants.ManageConnectionStrings);
		await base.DeleteDefaultConnectionStringAsync(id);
	}

	private async Task CheckTenantViewPermissionAsync()
	{
		if (!await _permissionChecker.IsGrantedAsync(TenantManagementPermissions.Tenants.Default))
		{
			
		}
	}

	private async Task CheckTenantUpdatePermissionAsync()
	{
		if (!await _permissionChecker.IsGrantedAsync(TenantManagementPermissions.Tenants.Update))
		{
			throw new AbpAuthorizationException("You are not authorized to update tenant information.");
		}
	}

	private async Task CheckHostTenantOperationAsync(string permissionName)
	{
		if (GetEffectiveTenantId() != null)
		{
			throw new AbpAuthorizationException("Only host users can manage tenants.");
		}

		if (!await _permissionChecker.IsGrantedAsync(permissionName))
		{
			throw new AbpAuthorizationException("You are not authorized to manage tenants.");
		}
	}

	private void CheckAuthenticatedTenantUser()
	{
		if (!CurrentUser.IsAuthenticated || GetEffectiveTenantId() == null)
		{
			throw new AbpAuthorizationException("You are not authorized to view tenant information.");
		}
	}

	private Guid? GetEffectiveTenantId()
	{
		return CurrentTenant.Id ?? CurrentUser.TenantId ?? GetTenantIdFromClaims();
	}

	private Guid? GetTenantIdFromClaims()
	{
		var principal = _currentPrincipalAccessor.Principal;
		var tenantIdValue = principal?.FindFirst(AbpClaimTypes.TenantId)?.Value
			?? principal?.FindFirst("tenantid")?.Value
			?? principal?.FindFirst("tenant_id")?.Value;

		return Guid.TryParse(tenantIdValue, out var tenantId) ? tenantId : null;
	}
}
