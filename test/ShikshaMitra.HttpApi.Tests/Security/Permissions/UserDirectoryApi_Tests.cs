using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Security.Permissions;

public class UserDirectoryTestFactory : ShikshaMitraApiTestFactory
{
    protected override bool InitializeTestData => false;
}

public class UserDirectoryApi_Tests(UserDirectoryTestFactory factory)
    : ShikshaMitraApiTestBase(factory), IClassFixture<UserDirectoryTestFactory>
{
    private const string DirectoryUrl = "/api/security/user-directory";
    private Guid _tenantId;

    [Fact]
    public async Task Directory_returns_assigned_roles_and_excludes_other_tenants()
    {
        var (role, user) = await CreateTargetAsync();
        var (foreignRole, foreignUser) = await CreateTargetAsync(Guid.NewGuid());
        var unassignedId = Guid.NewGuid();
        var secondRoleName = "directory-second-" + Guid.NewGuid().ToString("N");
        using (var scope = Factory.Services.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(_tenantId))
        using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
        {
            var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
            var secondRole = new IdentityRole(Guid.NewGuid(), secondRoleName, _tenantId);
            db.Set<IdentityRole>().Add(secondRole);
            (await db.Set<IdentityUser>().Include(item => item.Roles).SingleAsync(item => item.Id == user)).AddRole(secondRole.Id);
            db.Set<IdentityUser>().Add(new IdentityUser(unassignedId, "directory-empty-" + unassignedId.ToString("N"),
                unassignedId + "@example.invalid", _tenantId));
            await uow.CompleteAsync();
        }
        var result = await Client.GetFromJsonAsync<JsonElement>(DirectoryUrl + "?maxResultCount=1000");
        var rows = result.GetProperty("items").EnumerateArray().ToArray();
        var row = rows.Single(item => item.GetProperty("id").GetGuid() == user);
        row.GetProperty("roleNames").EnumerateArray().Select(item => item.GetString()).ShouldContain(role);
        row.GetProperty("roleNames").EnumerateArray().Select(item => item.GetString()).ShouldContain(secondRoleName);
        rows.Single(item => item.GetProperty("id").GetGuid() == unassignedId)
            .GetProperty("roleNames").GetArrayLength().ShouldBe(0);
        rows.ShouldNotContain(item => item.GetProperty("id").GetGuid() == foreignUser);
        rows.SelectMany(item => item.GetProperty("roleNames").EnumerateArray())
            .Select(item => item.GetString()).ShouldNotContain(foreignRole);
    }

    [Fact]
    public async Task Directory_preserves_identity_list_paging_and_authorization()
    {
        await CreateTargetAsync();
        await CreateTargetAsync();
        var first = await Client.GetFromJsonAsync<JsonElement>(DirectoryUrl + "?maxResultCount=1&sorting=userName,id");
        var second = await Client.GetFromJsonAsync<JsonElement>(DirectoryUrl + "?maxResultCount=1&skipCount=1&sorting=userName,id");
        first.GetProperty("totalCount").GetInt64().ShouldBeGreaterThan(1);
        first.GetProperty("items")[0].GetProperty("id").GetGuid()
            .ShouldNotBe(second.GetProperty("items")[0].GetProperty("id").GetGuid());
        using var denied = Factory.CreateClient();
        denied.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, _tenantId.ToString());
        denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");
        (await denied.GetAsync(DirectoryUrl)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var anonymous = CreateAnonymousClient();
        (await anonymous.GetAsync(DirectoryUrl)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<(string Role, Guid User)> CreateTargetAsync(Guid? tenantId = null)
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        if (_tenantId == Guid.Empty)
        {
            using var host = services.GetRequiredService<ICurrentTenant>().Change(null);
            var school = await services.GetRequiredService<ITenantManager>().CreateAsync("directory-" + Guid.NewGuid().ToString("N"));
            await services.GetRequiredService<IRepository<Tenant, Guid>>().InsertAsync(school);
            _tenantId = school.Id;
            Client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, _tenantId.ToString());
        }
        using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId ?? _tenantId);
        var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
        var name = "directory-" + Guid.NewGuid().ToString("N");
        var role = new IdentityRole(Guid.NewGuid(), name, tenantId ?? _tenantId);
        var user = new IdentityUser(Guid.NewGuid(), name, name + "@example.invalid", role.TenantId);
        user.AddRole(role.Id);
        db.Set<IdentityRole>().Add(role);
        db.Set<IdentityUser>().Add(user);
        await unit.CompleteAsync();
        return (name, user.Id);
    }
}
