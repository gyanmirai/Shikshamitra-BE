using System;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.TenantSettings.InitialSetup;
using ShikshaMitra.HR.Policies;

namespace ShikshaMitra.HostTenants;

[Authorize, RemoteService(false)]
public class HostTenantAppService(
	HostIdentityGuard guard, ITenantRepository tenants, ITenantManager tenantManager,
	IHostTenantQueryRepository query, TenantIdentityInitializer initializer, TenantStatusManager statusManager,
	IRepository<InstituteSetupProgress, Guid> setupProgress)
	: ShikshaMitraAppService, IHostTenantAppService
{
	public async Task<PagedResultDto<HostTenantDto>> GetListAsync(GetHostTenantsInput input)
	{
		await AuthorizeAsync(TenantManagementPermissions.Tenants.Default);
		Validate(input);
		var page = await query.GetPageAsync(input.Filter, input.IsActive, input.Sorting, input.SkipCount, input.MaxResultCount);
		return new PagedResultDto<HostTenantDto>(page.Count, page.Items.Select(Map).ToList());
	}

	public async Task<HostTenantDto> GetAsync(Guid id)
	{
		await AuthorizeAsync(TenantManagementPermissions.Tenants.Default);
		return Map(await tenants.GetAsync(id));
	}

	[DisableAuditing, UnitOfWork(isTransactional: true)]
	public async Task<HostTenantDto> CreateAsync(CreateHostTenantInput input)
	{
		await AuthorizeAsync(TenantManagementPermissions.Tenants.Create);
		Validate(input);
		var tenant = await tenantManager.CreateAsync(input.Name.Trim());
		ApplyDetails(tenant, input);
		// Only new production tenants opt in; existing schools and demo seeders stay untouched.
		tenant.SetProperty(HrStarterPolicyDefaults.TenantEnabledProperty, true);
		tenant.SetProperty(HostTenantConsts.IsActiveProperty, true);
		await tenants.InsertAsync(tenant);
		await CurrentUnitOfWork!.SaveChangesAsync();
		// Intentionally avoid IDataSeeder: production tenant creation must not import demo school data.
		await initializer.InitializeAsync(tenant.Id, input.AdminEmailAddress, input.AdminPassword);
		using (CurrentTenant.Change(tenant.Id))
			await setupProgress.InsertAsync(new InstituteSetupProgress(tenant.Id));
		return Map(tenant);
	}

	[UnitOfWork(isTransactional: true)]
	public async Task<HostTenantDto> UpdateAsync(Guid id, UpdateHostTenantInput input)
	{
		await AuthorizeAsync(TenantManagementPermissions.Tenants.Update);
		Validate(input);
		var tenant = await tenants.GetAsync(id);
		if (tenant.ConcurrencyStamp != input.ConcurrencyStamp) throw new AbpDbConcurrencyException();
		ApplyDetails(tenant, input);
		await tenants.UpdateAsync(tenant);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return Map(tenant);
	}

	[UnitOfWork(isTransactional: true)]
	public async Task<HostTenantDto> ChangeStatusAsync(Guid id, ChangeHostTenantStatusInput input)
	{
		await AuthorizeAsync(HostTenantConsts.ManageStatusPermission);
		Validate(input);
		var tenant = await tenants.GetAsync(id);
		await statusManager.ChangeAsync(tenant, input.IsActive, input.Reason, input.ConcurrencyStamp);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return Map(tenant);
	}

	private async Task AuthorizeAsync(string permission)
	{
		await guard.CheckAsync();
		await AuthorizationService.CheckAsync(permission);
	}
	private static void Validate(object input) => Validator.ValidateObject(input, new ValidationContext(input), true);
	private static void ApplyDetails(Tenant tenant, HostTenantDetailsInput input)
	{
		tenant.SetProperty("InstituteName", input.InstituteName.Trim());
		tenant.SetProperty("Email", input.Email.Trim());
		tenant.SetProperty("ContactNumber", input.ContactNumber.Trim());
		tenant.SetProperty("Address1", input.Address1.Trim());
		tenant.SetProperty("Address2", input.Address2?.Trim() ?? "");
		tenant.SetProperty("Address3", input.Address3?.Trim() ?? "");
		tenant.SetProperty("City", input.City.Trim());
		tenant.SetProperty("State", input.State.Trim());
		tenant.SetProperty("Country", input.Country.Trim());
		tenant.SetProperty("PostalCode", input.PostalCode.Trim());
	}
	private static HostTenantDto Map(Tenant tenant) => new()
	{
		Id = tenant.Id, Name = tenant.Name, ConcurrencyStamp = tenant.ConcurrencyStamp,
		CreationTime = tenant.CreationTime, IsActive = tenant.GetProperty(HostTenantConsts.IsActiveProperty, true),
		InstituteName = tenant.GetProperty<string>("InstituteName") ?? "",
		Email = tenant.GetProperty<string>("Email") ?? "", ContactNumber = tenant.GetProperty<string>("ContactNumber") ?? "",
		Address1 = tenant.GetProperty<string>("Address1") ?? "", Address2 = tenant.GetProperty<string>("Address2"),
		Address3 = tenant.GetProperty<string>("Address3"), City = tenant.GetProperty<string>("City") ?? "",
		State = tenant.GetProperty<string>("State") ?? "", Country = tenant.GetProperty<string>("Country") ?? "",
		PostalCode = tenant.GetProperty<string>("PostalCode") ?? ""
	};
}
