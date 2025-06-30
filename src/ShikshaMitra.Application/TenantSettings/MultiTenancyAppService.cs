using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.Local;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.TenantSettings;

public class MultiTenancyAppService : TenantAppService
{
	public MultiTenancyAppService(
		ITenantRepository tenantRepository,
		ITenantManager tenantManager,
		IDataSeeder dataSeeder,
		IDistributedEventBus distributedEventBus,
		ILocalEventBus localEventBus
	)
		: base(tenantRepository, tenantManager, dataSeeder, distributedEventBus, localEventBus) { }

	public override async Task<PagedResultDto<TenantDto>> GetListAsync(GetTenantsInput input)
	{
		var _currentTenant = CurrentTenant;
		var _currentUser = CurrentUser;

		// Host admin can see all tenants
		if (_currentTenant.Id == null && _currentUser.IsInRole("admin"))
		{
			return await base.GetListAsync(input);
		}

		// Tenant admin can see only their tenant
		if (_currentTenant.IsAvailable && _currentUser.IsInRole("admin"))
		{
			var currentTenant = await GetAsync(_currentTenant.Id!.Value);
			return new PagedResultDto<TenantDto>(1, new[] { currentTenant });
		}

		return new PagedResultDto<TenantDto>(0, new List<TenantDto>());
	}

	public override async Task<TenantDto> UpdateAsync(Guid id, TenantUpdateDto input)
	{
		var _currentTenant = CurrentTenant;
		var _currentUser = CurrentUser;

		// check if the current user is an admin
		return !_currentUser.IsInRole("admin")
				? throw new AbpAuthorizationException("Only admins can update tenant information.")
			: _currentTenant.IsAvailable && _currentTenant.Id != id
				? throw new AbpAuthorizationException("You are not allowed to update this tenant.")
			: await base.UpdateAsync(id, input);
	}

	public override async Task<TenantDto> GetAsync(Guid id)
	{
		var _currentTenant = CurrentTenant;
		var _currentUser = CurrentUser;

		// Host admin can see all tenants
		if (_currentTenant.Id == null && _currentUser.IsInRole("admin"))
		{
			return await base.GetAsync(id);
		}

		// Tenant admin can see only their tenant
		if (_currentTenant.IsAvailable && _currentUser.IsInRole("admin"))
		{
			if (_currentTenant.Id != id)
			{
				throw new AbpAuthorizationException("You are not allowed to view this tenant.");
			}
			return await base.GetAsync(id);
		}

		throw new AbpAuthorizationException("You are not authorized to view tenant information.");
	}
}
