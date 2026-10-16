using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Security.Permissions;

public partial class PermissionConsistencyApi_Tests
{
    private static string OverrideUrl(Guid user) => $"/api/shikshamitra-permissions/users/{user}/permissions";

    [Fact]
    public async Task User_overrides_survive_role_updates_and_follow_role_clears_them()
    {
        var (role, user) = await CreateTargetAsync();
        await SetAsync(Url("R", role), (View, true));
        await SetOverridesAsync(user, (View, "deny"), (Edit, "allow"));
        await AssertOverrideAsync(user, View, "deny", false);
        await AssertOverrideAsync(user, Edit, "allow", true);
        await SetAsync(Url("R", role), (View, false), (Edit, true));
        await SetAsync(Url("R", role), (View, true), (Edit, false));
        await AssertOverrideAsync(user, View, "deny", false);
        await AssertOverrideAsync(user, Edit, "allow", true);
        await AssertRealCheckerAsync(user, role, View, false);
        await AssertRealCheckerAsync(user, role, Edit, true);
        await SetOverridesAsync(user, (View, "inherit"), (Edit, "inherit"));
        await AssertOverrideAsync(user, View, "inherit", true);
        await AssertOverrideAsync(user, Edit, "inherit", false);
        await AssertRealCheckerAsync(user, role, View, true);
    }

    [Fact]
    public async Task Explicit_deny_blocks_alias_and_assignment_derived_access_in_single_and_batch_checks()
    {
        var (role, user) = await CreateTargetAsync();
        const string alias = "AbpIdentity.Users";
        const string derived = "Academics";
        await SetAsync(Url("R", role), ("AbpIdentity.Users.Delete", true));
        await SetOverridesAsync(user, (alias, "deny"), (derived, "deny"));
        await AssertRealCheckerAsync(user, role, alias, false);
        await AssertRealCheckerAsync(user, role, derived, false);
    }

    [Theory]
    [InlineData("Unknown.Future.Permission", "deny")]
    [InlineData("HR.Employee.EmployeeList.View", "invalid")]
    [InlineData("HR.Employee.EmployeeList.Edit", "deny")]
    [InlineData("Students.Records.Changes.Create", "allow")]
    [InlineData("Students.Admissions.Workflows.Approve", "deny")]
    public async Task Override_batches_validate_before_any_mutation(string name, string state)
    {
        var (_, user) = await CreateTargetAsync();
        using var response = await Client.PutAsJsonAsync(OverrideUrl(user), new { permissions = new[] {
            new { name = Edit, state = "allow" }, new { name, state } } });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        await AssertOverrideAsync(user, Edit, "inherit", false);
    }

    [Fact]
    public async Task Overrides_preserve_management_authorization_and_tenant_isolation()
    {
        var (_, foreignUser) = await CreateTargetAsync(Guid.NewGuid());
        (await Client.GetAsync(OverrideUrl(foreignUser))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.PutAsJsonAsync(OverrideUrl(foreignUser), new { permissions = new[] { new { name = Edit, state = "allow" } } }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var denied = Factory.CreateClient();
        denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");
        (await denied.PutAsJsonAsync(OverrideUrl(Guid.Parse(TestAuthHandler.TestUserId)), new { permissions = new[] { new { name = Edit, state = "allow" } } }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var anonymous = CreateAnonymousClient();
        (await anonymous.GetAsync(OverrideUrl(foreignUser))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task SetOverridesAsync(Guid user, params (string Name, string State)[] changes)
    {
        using var response = await Client.PutAsJsonAsync(OverrideUrl(user), new { permissions = changes.Select(change => new { name = change.Name, state = change.State }) });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private async Task AssertOverrideAsync(Guid user, string name, string state, bool granted)
    {
        var result = await Client.GetFromJsonAsync<UserPermissionOverridesDto>(OverrideUrl(user), JsonOptions);
        result!.Overrides.GetValueOrDefault(name, "inherit").ShouldBe(state);
        result.Groups.SelectMany(group => group.Permissions).Single(permission => permission.Name == name).IsGranted.ShouldBe(granted);
        (await ReadAsync(Url("U", user.ToString()))).Single(permission => permission.Name == name).IsGranted.ShouldBe(granted);
    }

    private async Task AssertRealCheckerAsync(Guid user, string role, string name, bool granted)
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
        using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, user.ToString()), new Claim(AbpClaimTypes.Role, role) }, "test"));
        using var current = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(principal);
        IPermissionChecker checker = ActivatorUtilities.CreateInstance<ShikshaMitraPermissionChecker>(services, new AssignedTeacher());
        (await checker.IsGrantedAsync(name)).ShouldBe(granted);
        (await checker.IsGrantedAsync(principal, name)).ShouldBe(granted);
        ((await checker.IsGrantedAsync(new[] { name })).Result[name] == PermissionGrantResult.Granted).ShouldBe(granted);
        ((await checker.IsGrantedAsync(principal, new[] { name })).Result[name] == PermissionGrantResult.Granted).ShouldBe(granted);
    }

    private sealed class AssignedTeacher : IClassTeacherAccessService
    {
        public Task<bool> HasAssignmentAsync(Guid userId) => Task.FromResult(true);
        public Task InvalidateAsync(IEnumerable<Guid> userIds) => Task.CompletedTask;
    }

}
