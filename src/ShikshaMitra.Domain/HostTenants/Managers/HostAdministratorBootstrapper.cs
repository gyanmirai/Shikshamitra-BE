using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Seeders;
using Volo.Abp.Domain.Services;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using IdentityRole = Volo.Abp.Identity.IdentityRole;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace ShikshaMitra.HostTenants;

public class HostAdministratorBootstrapper(
	IdentityUserManager users, IdentityRoleManager roles, IPermissionGrantSeedRepository grants,
	ICurrentTenant currentTenant, IUnitOfWorkManager unitOfWorkManager, IConfiguration configuration) : DomainService
{
	public async Task<IdentityUser> EnsureAsync()
	{
		using (currentTenant.Change(null))
		using (var unit = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
		{
			var name = configuration["ShikshaMitra:HostAdmin:UserName"] ?? "admin";
			var existing = await users.FindByNameAsync(name);
			if (existing != null)
			{
				// Reruns must not alter passwords, memberships, or revoked grants.
				await unit.CompleteAsync();
				return existing;
			}
			var email = configuration["ShikshaMitra:HostAdmin:Email"];
			var password = configuration["ShikshaMitra:HostAdmin:Password"];
			if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
				throw new InvalidOperationException("A missing host administrator requires ShikshaMitra:HostAdmin:Email and Password from secure configuration.");
			if (await roles.FindByNameAsync("admin") == null)
			{
				var role = new IdentityRole(GuidGenerator.Create(), "admin");
				(await roles.CreateAsync(role)).CheckErrors();
				await grants.InsertRoleGrantsIgnoringDuplicatesAsync(role.Name, [
					TenantManagementPermissions.Tenants.Default, TenantManagementPermissions.Tenants.Create,
					TenantManagementPermissions.Tenants.Update, HostTenantConsts.ManageStatusPermission], null);
			}
			var user = new IdentityUser(GuidGenerator.Create(), name, email);
			(await users.CreateAsync(user, password)).CheckErrors();
			(await users.AddToRoleAsync(user, "admin")).CheckErrors();
			await unit.CompleteAsync();
			return user;
		}
	}
}
