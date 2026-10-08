using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HostTenants;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

[Collection(ApiTestCollection.Name), Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public class HostTenantApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Url = "/api/host/tenants";
	private readonly Guid hostUserId = Guid.NewGuid();
	private const string Password = "New-School-Secret-29!";

	[Fact]
	public async Task View_and_update_permissions_do_not_grant_status_and_legacy_updates_cannot_bypass_audit()
	{
		using var host = await HostAsync(); var created = await CreateAsync(host);
		using var reader = HostClient(TenantManagementPermissions.Tenants.Default);
		(await reader.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await reader.PostAsJsonAsync(Url, Input("denied"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var editor = HostClient(TenantManagementPermissions.Tenants.Update);
		(await editor.PutAsJsonAsync($"{Url}/{created.Id}/status", Status(created, false))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var legacy = new TenantUpdateDto { Name = created.Name, ConcurrencyStamp = created.ConcurrencyStamp };
		legacy.ExtraProperties["InstituteName"] = created.InstituteName; legacy.ExtraProperties["Email"] = created.Email;
		legacy.ExtraProperties["ContactNumber"] = created.ContactNumber; legacy.ExtraProperties["Address1"] = created.Address1;
		legacy.ExtraProperties["City"] = created.City; legacy.ExtraProperties["State"] = created.State;
		legacy.ExtraProperties["Country"] = created.Country; legacy.ExtraProperties["PostalCode"] = created.PostalCode;
		legacy.ExtraProperties[HostTenantConsts.IsActiveProperty] = false;
		var response = await host.PutAsJsonAsync("/api/multi-tenancy/tenants/" + created.Id, legacy);
		response.IsSuccessStatusCode.ShouldBeFalse();
		(await response.Content.ReadAsStringAsync()).ShouldContain(HostTenantConsts.StatusReadOnly);
		(await host.GetFromJsonAsync<HostTenantDto>($"{Url}/{created.Id}"))!.IsActive.ShouldBeTrue();
	}

	[Fact]
	public async Task Host_context_search_paging_and_detail_updates_use_typed_contracts()
	{
		using var host = await HostAsync();
		var context = (await host.GetFromJsonAsync<HostSessionContextDto>("/api/host/session-context"))!;
		context.AccountMode.ShouldBe("host"); context.TenantId.ShouldBeNull(); context.CanManageTenantStatus.ShouldBeTrue();
		var prefix = "search" + Guid.NewGuid().ToString("N");
		var first = await CreateAsync(host, prefix + "a"); await CreateAsync(host, prefix + "b");
		var page = (await host.GetFromJsonAsync<Volo.Abp.Application.Dtos.PagedResultDto<HostTenantDto>>(
			$"{Url}?filter={prefix}&skipCount=1&maxResultCount=1&sorting=name%20asc"))!;
		page.TotalCount.ShouldBe(2); page.Items.Count.ShouldBe(1); page.Items[0].Name.ShouldBe(prefix + "b");
		var update = new UpdateHostTenantInput { InstituteName = "Renamed synthetic school", Email = "changed@example.test",
			ContactNumber = first.ContactNumber, Address1 = first.Address1, City = first.City, State = first.State,
			Country = first.Country, PostalCode = first.PostalCode, ConcurrencyStamp = first.ConcurrencyStamp };
		var saved = await host.PutAsJsonAsync($"{Url}/{first.Id}", update);
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		var changed = (await saved.Content.ReadFromJsonAsync<HostTenantDto>())!;
		changed.Name.ShouldBe(first.Name); changed.InstituteName.ShouldBe(update.InstituteName); changed.IsActive.ShouldBeTrue();
		(await host.PutAsJsonAsync($"{Url}/{first.Id}", update)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var search = (await host.GetFromJsonAsync<Volo.Abp.Application.Dtos.PagedResultDto<HostTenantDto>>(Url + "?filter=Renamed%20synthetic"))!;
		search.Items.ShouldContain(x => x.Id == first.Id);
	}

	[Fact]
	public async Task Host_creation_is_atomic_and_school_admin_has_no_host_permissions()
	{
		using var host = await HostAsync();
		var code = "rollback" + Guid.NewGuid().ToString("N");
		var input = Input(code); input.AdminPassword = "abcdefgh";
		var invalid = await host.PostAsJsonAsync(Url, input);
		invalid.IsSuccessStatusCode.ShouldBeFalse();
		await InTenantAsync(null, async services =>
		{
			(await services.GetRequiredService<ITenantRepository>().FindByNameAsync(code.ToUpperInvariant())).ShouldBeNull();
		});
		var created = await CreateAsync(host);
		created.IsActive.ShouldBeTrue(); created.Address2.ShouldBeNullOrEmpty();
		await InTenantAsync(created.Id, async services =>
		{
			var users = services.GetRequiredService<IdentityUserManager>();
			var admin = await users.FindByNameAsync("admin"); admin.ShouldNotBeNull();
			(await users.CheckPasswordAsync(admin, Password)).ShouldBeTrue();
			(await users.IsInRoleAsync(admin, "admin")).ShouldBeTrue();
			var definitions = services.GetRequiredService<IPermissionDefinitionManager>();
			(await definitions.GetAsync(HostTenantConsts.ManageStatusPermission)).MultiTenancySide.ShouldBe(MultiTenancySides.Host);
			var db = services.GetRequiredService<ShikshaMitraDbContext>();
			var grants = await db.Set<Volo.Abp.PermissionManagement.PermissionGrant>().Where(x => x.TenantId == created.Id).ToListAsync();
			grants.Any(x => x.Name == HostTenantConsts.ManageStatusPermission || x.Name == TenantManagementPermissions.Tenants.Create).ShouldBeFalse();
		});
	}

	[Fact]
	public async Task Host_actions_require_exact_grants_and_reject_tenant_and_fabricated_host_identity()
	{
		using var host = await HostAsync();
		var created = await CreateAsync(host);
		using var denied = HostClient("__none__");
		(await denied.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await denied.PostAsJsonAsync(Url, Input("denied"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await denied.PutAsJsonAsync($"{Url}/{created.Id}/status", Status(created, false))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var school = Factory.CreateClient();
		(await school.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var forged = HostClient(); forged.DefaultRequestHeaders.Remove(TestAuthHandler.TestUserIdHeader);
		forged.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
		(await forged.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var anonymous = CreateAnonymousClient();
		(await anonymous.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		await InTenantAsync(Guid.Parse(TestAuthHandler.TestTenantId), async services =>
		{
			var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] {
				new Claim(AbpClaimTypes.UserId, TestAuthHandler.TestUserId),
				new Claim(AbpClaimTypes.TenantId, TestAuthHandler.TestTenantId), new Claim(AbpClaimTypes.Role, "super-admin") }, "test"));
			using (services.GetRequiredService<ICurrentPrincipalAccessor>().Change(principal))
				await Should.ThrowAsync<AbpAuthorizationException>(() => services.GetRequiredService<IHostTenantAppService>().GetAsync(created.Id));
		});
	}

	[Fact]
	public async Task Status_is_audited_concurrency_protected_and_enforced_after_cache_warmup()
	{
		using var host = await HostAsync(); var created = await CreateAsync(host);
		using var school = Factory.CreateClient();
		school.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, created.Id.ToString());
		(await school.GetAsync("/api/multi-tenancy/tenants/" + created.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);
		var noReason = Status(created, false); noReason.Reason = " ";
		(await host.PutAsJsonAsync($"{Url}/{created.Id}/status", noReason)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var response = await host.PutAsJsonAsync($"{Url}/{created.Id}/status", Status(created, false));
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var inactive = (await response.Content.ReadFromJsonAsync<HostTenantDto>())!;
		inactive.IsActive.ShouldBeFalse();
		(await host.PutAsJsonAsync($"{Url}/{created.Id}/status", Status(created, true))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var blocked = await school.GetAsync("/api/multi-tenancy/tenants/" + created.Id);
		blocked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await blocked.Content.ReadAsStringAsync()).ShouldContain("TENANT_INACTIVE");
		await InTenantAsync(null, async services =>
		{
			var history = await services.GetRequiredService<IRepository<TenantStatusChange, Guid>>().GetListAsync(x => x.TargetTenantId == created.Id);
			history.Count.ShouldBe(1); history[0].CreatorId.ShouldBe(hostUserId);
			history[0].PreviousStatus.ShouldBeTrue(); history[0].IsActive.ShouldBeFalse();
			history[0].Reason.ShouldBe("Contract suspended");
		});
		var activate = await host.PutAsJsonAsync($"{Url}/{created.Id}/status", Status(inactive, true));
		activate.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await school.GetAsync("/api/multi-tenancy/tenants/" + created.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Deactivation_blocks_native_password_and_refresh_even_without_tenant_header()
	{
		using var host = await HostAsync(); var created = await CreateAsync(host);
		var clientId = "tenant-token-" + Guid.NewGuid().ToString("N");
		await InTenantAsync(null, async services => await services.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(new OpenIddictApplicationDescriptor {
			ClientId = clientId, ClientType = OpenIddictConstants.ClientTypes.Public,
			Permissions = { OpenIddictConstants.Permissions.Endpoints.Token,
				OpenIddictConstants.Permissions.GrantTypes.Password, OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
				OpenIddictConstants.Permissions.Scopes.Profile, OpenIddictConstants.Permissions.Scopes.Email,
				OpenIddictConstants.Permissions.Prefixes.Scope + "ShikshaMitra" } }));
		using var login = CreateAnonymousClient(); login.BaseAddress = new Uri("https://localhost"); login.DefaultRequestHeaders.Add("__tenant", created.Name);
		var credentials = new Dictionary<string, string> { ["client_id"] = clientId, ["grant_type"] = "password", ["username"] = "admin",
			["password"] = Password, ["scope"] = "openid profile email offline_access ShikshaMitra" };
		var initial = await login.PostAsync("/connect/token", new FormUrlEncodedContent(credentials));
		initial.StatusCode.ShouldBe(HttpStatusCode.OK, await initial.Content.ReadAsStringAsync());
		using var token = JsonDocument.Parse(await initial.Content.ReadAsStringAsync());
		(await host.PutAsJsonAsync($"{Url}/{created.Id}/status", Status(created, false))).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await login.PostAsync("/connect/token", new FormUrlEncodedContent(credentials))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		login.DefaultRequestHeaders.Remove("__tenant");
		var refresh = await login.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string> {
			["client_id"] = clientId, ["grant_type"] = "refresh_token", ["refresh_token"] = token.RootElement.GetProperty("refresh_token").GetString()! }));
		refresh.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await refresh.Content.ReadAsStringAsync());
		(await refresh.Content.ReadAsStringAsync()).ShouldContain("invalid_grant");
	}

	private async Task<HttpClient> HostAsync()
	{
		await InTenantAsync(null, async services =>
		{
			var repository = services.GetRequiredService<IIdentityUserRepository>();
			if (await repository.FindAsync(hostUserId) == null)
				await repository.InsertAsync(new IdentityUser(hostUserId, "host" + hostUserId.ToString("N"), hostUserId + "@example.test"));
		});
		return HostClient();
	}
	private HttpClient HostClient(string? grants = null)
	{
		var client = Factory.CreateClient(); client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, "host");
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, hostUserId.ToString());
		if (grants != null) client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, grants);
		return client;
	}
	private async Task InTenantAsync(Guid? tenantId, Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope(); using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(scope.ServiceProvider); await unit.CompleteAsync();
	}
	private static CreateHostTenantInput Input(string code) => new() { Name = code, InstituteName = "Synthetic Host School", Email = code + "@example.test",
		ContactNumber = "9876543210", Address1 = "Synthetic Test Road", City = "Surat", State = "Gujarat", Country = "India", PostalCode = "395001",
		AdminEmailAddress = "admin@" + code + ".example.test", AdminPassword = Password };
	private async Task<HostTenantDto> CreateAsync(HttpClient host, string? code = null)
	{
		var response = await host.PostAsJsonAsync(Url, Input(code ?? "host" + Guid.NewGuid().ToString("N")));
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<HostTenantDto>())!;
	}
	private static ChangeHostTenantStatusInput Status(HostTenantDto tenant, bool active) => new() { IsActive = active, Reason = "Contract suspended", ConcurrencyStamp = tenant.ConcurrencyStamp };
}
