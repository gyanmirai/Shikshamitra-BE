using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Security.Permissions;

public partial class PermissionConsistencyApi_Tests
{
	[Fact]
	public async Task Admin_catalogues_are_fully_granted_and_all_write_routes_are_locked()
	{
		var (tenantId, userId) = await CreateAdminTargetAsync();
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenantId.ToString());
		foreach (var url in new[] { Url("R", "admin"), Url("U", userId.ToString()) })
		{
			var catalogue = await client.GetFromJsonAsync<Volo.Abp.PermissionManagement.GetPermissionListResultDto>(url, JsonOptions);
			var entries = catalogue!.Groups.SelectMany(group => group.Permissions).ToArray();
			entries.ShouldNotBeEmpty();
			entries.Where(entry => entry.AllowedProviders.Count == 0 || entry.AllowedProviders.Contains("R"))
				.ShouldAllBe(entry => entry.IsGranted);
			using var response = await client.PutAsJsonAsync(url, new { permissions = new[] { new { name = View, isGranted = false } } });
			response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
			(await response.Content.ReadAsStringAsync()).ShouldContain("ADMIN_PERMISSIONS_READ_ONLY");
		}
		using var overrides = await client.PutAsJsonAsync(OverrideUrl(userId), new { permissions = new[] { new { name = View, state = "deny" } } });
		overrides.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await overrides.Content.ReadAsStringAsync());
		var result = await client.GetFromJsonAsync<System.Text.Json.JsonElement>(OverrideUrl(userId));
		result.GetProperty("isReadOnly").GetBoolean().ShouldBeTrue();
	}

	[Fact]
	public async Task Admin_checks_use_live_membership_and_ignore_old_denials_without_granting_host_permissions()
	{
		var (tenantId, userId) = await CreateAdminTargetAsync();
		using (var scope = Factory.Services.CreateScope())
		{
			using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
			using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
			await scope.ServiceProvider.GetRequiredService<IUserPermissionDenyStore>().SetDeniedAsync(userId, View, true);
			await uow.CompleteAsync();
		}
		await CheckAdminAsync(tenantId, userId, "ordinary-role", View, true);
		await CheckAdminAsync(tenantId, userId, "admin", "Unknown.Future.Permission", false);
		await CheckAdminAsync(tenantId, userId, "admin", "AbpTenantManagement.Tenants.Create", false);
		await CheckAdminAsync(Guid.NewGuid(), userId, "admin", View, false);
		using (var scope = Factory.Services.CreateScope())
		{
			using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
			using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
			var repository = scope.ServiceProvider.GetRequiredService<IIdentityUserRepository>();
			var user = await repository.GetAsync(userId);
			user.Roles.Clear();
			await repository.UpdateAsync(user);
			await uow.CompleteAsync();
		}
		await CheckAdminAsync(tenantId, userId, "admin", View, false);
	}

	private async Task CheckAdminAsync(Guid tenantId, Guid userId, string roleClaim, string permission, bool granted)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()),
			new Claim(AbpClaimTypes.Role, roleClaim) }, "test"));
		using var current = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(principal);
		IPermissionChecker checker = ActivatorUtilities.CreateInstance<ShikshaMitraPermissionChecker>(services);
		(await checker.IsGrantedAsync(permission)).ShouldBe(granted);
		(await checker.IsGrantedAsync(principal, permission)).ShouldBe(granted);
		((await checker.IsGrantedAsync(new[] { permission })).Result[permission] == PermissionGrantResult.Granted).ShouldBe(granted);
		((await checker.IsGrantedAsync(principal, new[] { permission })).Result[permission] == PermissionGrantResult.Granted).ShouldBe(granted);
	}

	private async Task<(Guid Tenant, Guid User)> CreateAdminTargetAsync()
	{
		using var scope = Factory.Services.CreateScope();
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var school = await scope.ServiceProvider.GetRequiredService<Volo.Abp.TenantManagement.TenantManager>()
			.CreateAsync("admin-policy-" + Guid.NewGuid().ToString("N"));
		var tenantId = school.Id;
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		db.Set<Volo.Abp.TenantManagement.Tenant>().Add(school);
		var role = new IdentityRole(Guid.NewGuid(), "admin", tenantId);
		var user = new IdentityUser(Guid.NewGuid(), "admin-target", "admin-target@example.invalid", tenantId);
		user.AddRole(role.Id);
		db.Set<IdentityRole>().Add(role);
		db.Set<IdentityUser>().Add(user);
		await uow.CompleteAsync();
		return (tenantId, user.Id);
	}
}
