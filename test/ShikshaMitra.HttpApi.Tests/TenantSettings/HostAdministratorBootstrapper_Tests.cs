using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HostTenants;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace ShikshaMitra.TenantSettings;

[Collection(ApiTestCollection.Name), Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public class HostAdministratorBootstrapper_Tests(ShikshaMitraApiTestFactory factory)
{
	[Fact]
	public async Task Existing_host_account_keeps_password_memberships_and_grants_on_rerun()
	{
		using var scope = factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(null);
		var name = "existing" + Guid.NewGuid().ToString("N");
		var users = services.GetRequiredService<IdentityUserManager>();
		Guid id;
		using (var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var user = new IdentityUser(Guid.NewGuid(), name, name + "@example.test");
			(await users.CreateAsync(user, "Preserved-Password-29!")).CheckErrors();
			id = user.Id;
			await unit.CompleteAsync();
		}
		var configuration = services.GetRequiredService<IConfiguration>();
		await WithConfigurationAsync(configuration, name, null, null, async () =>
		{
			var initializer = services.GetRequiredService<HostAdministratorBootstrapper>();
			(await initializer.EnsureAsync()).Id.ShouldBe(id);
			(await initializer.EnsureAsync()).Id.ShouldBe(id);
		});
		using var read = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var saved = (await users.FindByIdAsync(id.ToString()))!;
		(await users.CheckPasswordAsync(saved, "Preserved-Password-29!")).ShouldBeTrue();
		(await users.GetRolesAsync(saved)).ShouldBeEmpty();
		await read.CompleteAsync();
	}

	[Fact]
	public async Task Missing_host_is_created_once_without_restoring_existing_admin_role_grants()
	{
		using var scope = factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(null);
		var name = "bootstrap" + Guid.NewGuid().ToString("N");
		var configuration = services.GetRequiredService<IConfiguration>();
		var units = services.GetRequiredService<IUnitOfWorkManager>();
		int before;
		using (var read = units.Begin(requiresNew: true))
		{
			before = await services.GetRequiredService<ShikshaMitraDbContext>().Set<Volo.Abp.PermissionManagement.PermissionGrant>()
				.CountAsync(x => x.TenantId == null && x.ProviderName == "R" && x.ProviderKey == "admin");
			(await services.GetRequiredService<IdentityRoleManager>().FindByNameAsync("admin")).ShouldNotBeNull();
			await read.CompleteAsync();
		}
		Guid id = Guid.Empty;
		await WithConfigurationAsync(configuration, name, name + "@example.test", "Bootstrap-Password-29!", async () =>
		{
			var initializer = services.GetRequiredService<HostAdministratorBootstrapper>();
			id = (await initializer.EnsureAsync()).Id;
			(await initializer.EnsureAsync()).Id.ShouldBe(id);
		});
		using var verify = units.Begin(requiresNew: true);
		var users = services.GetRequiredService<IdentityUserManager>();
		var user = (await users.FindByIdAsync(id.ToString()))!;
		(await users.CheckPasswordAsync(user, "Bootstrap-Password-29!")).ShouldBeTrue();
		(await users.GetRolesAsync(user)).ShouldBe(new[] { "admin" });
		(await services.GetRequiredService<ShikshaMitraDbContext>().Set<Volo.Abp.PermissionManagement.PermissionGrant>()
			.CountAsync(x => x.TenantId == null && x.ProviderName == "R" && x.ProviderKey == "admin")).ShouldBe(before);
		await verify.CompleteAsync();
	}

	[Fact]
	public async Task Missing_host_requires_secure_configuration_and_creates_no_partial_account()
	{
		using var scope = factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var name = "missing" + Guid.NewGuid().ToString("N");
		await WithConfigurationAsync(services.GetRequiredService<IConfiguration>(), name, null, null,
			() => Should.ThrowAsync<InvalidOperationException>(() => services.GetRequiredService<HostAdministratorBootstrapper>().EnsureAsync()));
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(null);
		using var read = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		(await services.GetRequiredService<IdentityUserManager>().FindByNameAsync(name)).ShouldBeNull();
		await read.CompleteAsync();
	}

	private static async Task WithConfigurationAsync(IConfiguration configuration, string name, string? email, string? password, Func<Task> action)
	{
		var keys = new[] { "ShikshaMitra:HostAdmin:UserName", "ShikshaMitra:HostAdmin:Email", "ShikshaMitra:HostAdmin:Password" };
		var original = keys.Select(key => configuration[key]).ToArray();
		try
		{
			configuration[keys[0]] = name; configuration[keys[1]] = email; configuration[keys[2]] = password;
			await action();
		}
		finally { for (var index = 0; index < keys.Length; index++) configuration[keys[index]] = original[index]; }
	}
}
