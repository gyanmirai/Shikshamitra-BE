using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Security.Permissions;

[Collection(ApiTestCollection.Name)]
public class PermissionConsistencyApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
    private const string Edit = ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit;
    private const string View = ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View;
    private const string Activate = ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.Activate;
    private static string Url(string provider, string key) =>
        $"/api/permission-management/permissions?providerName={provider}&providerKey={Uri.EscapeDataString(key)}";

    [Fact]
    public async Task Role_and_user_round_trips_preserve_independent_and_inherited_grants()
    {
        var (role, user) = await CreateTargetAsync();
        var roleUrl = Url("R", role);
        var userUrl = Url("U", user.ToString());
        await SetAsync(roleUrl, (Edit, true), (Activate, true));
        var roleGrants = await ReadAsync(roleUrl);
        roleGrants.Single(x => x.Name == Edit).IsGranted.ShouldBeTrue();
        roleGrants.Single(x => x.Name == View).IsGranted.ShouldBeFalse();
        await SetAsync(userUrl, (Edit, true));
        var userGrants = await ReadAsync(userUrl);
        var sources = userGrants.Single(x => x.Name == Edit).GrantedProviders;
        sources.ShouldContain(x => x.ProviderName == "U" && x.ProviderKey == user.ToString());
        sources.ShouldContain(x => x.ProviderName == "R" && x.ProviderKey == role);
        await SetAsync(userUrl, (Edit, false));
        var inherited = (await ReadAsync(userUrl)).Single(x => x.Name == Edit);
        inherited.IsGranted.ShouldBeTrue();
        inherited.GrantedProviders.ShouldNotContain(x => x.ProviderName == "U");
        inherited.GrantedProviders.ShouldContain(x => x.ProviderName == "R" && x.ProviderKey == role);
        await SetAsync(roleUrl, (Edit, false));
        (await ReadAsync(userUrl)).Single(x => x.Name == Edit).IsGranted.ShouldBeFalse();
        (await ReadAsync(roleUrl)).Single(x => x.Name == Activate).IsGranted.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Unknown.Future.Permission")]
    [InlineData("HR.Employee.EmployeeList.Create")]
    [InlineData("hr.employee.employeelist.edit")]
    public async Task Invalid_batches_are_atomic_and_never_guess_aliases(string invalid)
    {
        var (role, _) = await CreateTargetAsync();
        var url = Url("R", role);
        using var response = await Client.PutAsJsonAsync(url, new { permissions = new[] {
            new { name = Edit, isGranted = true }, new { name = invalid, isGranted = true } } });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        (await ReadAsync(url)).Single(x => x.Name == Edit).IsGranted.ShouldBeFalse();
    }

    [Fact]
    public async Task Duplicate_entries_and_ineligible_host_permissions_are_rejected_before_writing()
    {
        var (role, _) = await CreateTargetAsync();
        var url = Url("R", role);
        foreach (var batch in new[] {
            new[] { new { name = Edit, isGranted = true }, new { name = Edit, isGranted = false } },
            new[] { new { name = Edit, isGranted = true }, new { name = "AbpTenantManagement.Tenants.Create", isGranted = true } }
        })
        {
            using var response = await Client.PutAsJsonAsync(url, new { permissions = batch });
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
            (await ReadAsync(url)).Single(x => x.Name == Edit).IsGranted.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Both_read_routes_deny_unauthenticated_and_unauthorized_management_but_allow_self_reads()
    {
        var (role, _) = await CreateTargetAsync();
        using var anonymous = CreateAnonymousClient();
        using var denied = Factory.CreateClient();
        denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");
        foreach (var url in new[] { Url("R", role), $"/api/shikshamitra-permissions/permissions?providerName=R&providerKey={Uri.EscapeDataString(role)}" })
        {
            (await anonymous.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await denied.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
        var self = Url("U", TestAuthHandler.TestUserId);
        (await denied.GetAsync(self)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await denied.PutAsJsonAsync(self, new { permissions = new[] { new { name = Edit, isGranted = true } } }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cross_tenant_provider_keys_are_not_readable_or_writable()
    {
        var (role, user) = await CreateTargetAsync(Guid.NewGuid());
        foreach (var url in new[] { Url("R", role), Url("U", user.ToString()) })
        {
            (await Client.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await Client.PutAsJsonAsync(url, new { permissions = new[] { new { name = Edit, isGranted = true } } }))
                .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task Unproxied_application_calls_cannot_bypass_esi_authorization()
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<IHttpContextAccessor>();
        var oldContext = context.HttpContext;
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(AbpClaimTypes.UserId, TestAuthHandler.TestUserId),
            new Claim(AbpClaimTypes.TenantId, TestAuthHandler.TestTenantId)
        }, TestAuthHandler.SchemeName));
        using var current = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(principal);
        context.HttpContext = new DefaultHttpContext { User = principal };
        context.HttpContext.Request.Headers[AlwaysGrantPermissionChecker.TestPermissionsHeader] = "__none__";
        try
        {
            var app = ActivatorUtilities.CreateInstance<EsiStatutoryAppService>(services);
            app.LazyServiceProvider = services.GetRequiredService<IAbpLazyServiceProvider>();
            await Should.ThrowAsync<AbpAuthorizationException>(() => app.GetCurrentRuleAsync());
            await Should.ThrowAsync<AbpAuthorizationException>(() => app.GetEstablishmentAsync());
            await Should.ThrowAsync<AbpAuthorizationException>(() => app.ConfigureEstablishmentAsync(new()));
            await Should.ThrowAsync<AbpAuthorizationException>(() => app.GetComponentTreatmentsAsync());
            await Should.ThrowAsync<AbpAuthorizationException>(() => app.UpdateComponentTreatmentsAsync(new()));
            await Should.ThrowAsync<AbpAuthorizationException>(() => app.GetEnrollmentAsync(Guid.NewGuid()));
            await Should.ThrowAsync<AbpAuthorizationException>(() => app.UpdateEnrollmentAsync(Guid.NewGuid(), new()));
            await Should.ThrowAsync<AbpAuthorizationException>(() => app.GetOverridesAsync(Guid.NewGuid()));
            await Should.ThrowAsync<AbpAuthorizationException>(() => app.CreateOverrideAsync(Guid.NewGuid(), new()));
            await Should.ThrowAsync<AbpAuthorizationException>(() => app.PreviewAsync(new()));
            await Should.ThrowAsync<AbpAuthorizationException>(() => app.ActivateAsync(new()));
        }
        finally { context.HttpContext = oldContext; }
    }

    [Fact]
    public async Task Esi_classification_edit_does_not_require_the_separate_view_action()
    {
        const string url = "/api/payroll/statutory-schemes/esi/component-treatments";
        var treatments = await Client.GetFromJsonAsync<PayrollComponentStatutoryTreatmentDto[]>(
            url + "?effectiveOn=2026-08-01", JsonOptions);
        treatments.ShouldNotBeNull();
        treatments.ShouldNotBeEmpty();
        var treatment = treatments.First();
        using var editor = Factory.CreateClient();
        editor.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
            ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.Edit);
        using var response = await editor.PutAsJsonAsync(url, new UpdatePayrollComponentStatutoryTreatmentsDto
        {
            Items = [new PayrollComponentStatutoryTreatmentDto
            {
                ComponentId = treatment.ComponentId,
                Treatment = treatment.Treatment,
                EffectiveFrom = treatment.EffectiveFrom,
                EffectiveTo = treatment.EffectiveTo
            }]
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var saved = await response.Content.ReadFromJsonAsync<PayrollComponentStatutoryTreatmentDto[]>(JsonOptions);
        saved.ShouldNotBeNull();
        saved.Single(x => x.ComponentId == treatment.ComponentId).Treatment.ShouldBe(treatment.Treatment);
        (await editor.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Esi_activation_only_grant_reaches_freshness_check_without_granting_view_or_edit()
    {
        const string baseUrl = "/api/payroll/statutory-schemes/esi";
        using var activator = Factory.CreateClient();
        activator.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, Activate);
        using var response = await activator.PostAsJsonAsync(baseUrl + "/activate", new ActivateStatutorySchemeDto
        {
            MonthYear = "08-2026", PreviewFingerprint = new string('0', 64)
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).ShouldContain("ESI_PREVIEW_STALE");
        (await activator.GetAsync(baseUrl + "/establishment")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await activator.PutAsJsonAsync(baseUrl + "/establishment", new ConfigureStatutoryEstablishmentDto()))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Esi_employee_action_grants_cannot_bypass_scope_or_cross_tenant_boundaries()
    {
        var (_, userId) = await CreateTargetAsync();
        Guid employeeId;
        using (var scope = Factory.Services.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
        using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
        {
            var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
            employeeId = await db.Set<Employee>().Where(x => x.UserId == Guid.Parse(TestAuthHandler.TestUserId))
                .Select(x => x.Id).SingleAsync();
        }
        var url = $"/api/payroll/statutory-schemes/esi/employees/{employeeId}";
        var before = await Client.GetStringAsync(url + "/enrollment");
        var overridesBefore = await Client.GetStringAsync(url + "/overrides");
        using var unscoped = CreateClientAs(userId);
        unscoped.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(',',
            ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.View,
            ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.ManageEnrollment,
            ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.ApproveOverride));
        (await unscoped.GetAsync(url + "/enrollment")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var input = new UpdateEmployeeStatutoryEnrollmentDto { Status = "RegistrationPending", StatusReason = "Must not persist" };
        (await unscoped.PutAsJsonAsync(url + "/enrollment", input)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var change = new CreateStatutoryOverrideDto { Reason = "Must not persist", OverrideType = "ForceCovered",
            EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), EffectiveTo = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc) };
        (await unscoped.PostAsJsonAsync(url + "/overrides", change)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        unscoped.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
        (await unscoped.GetAsync(url + "/enrollment")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await unscoped.PutAsJsonAsync(url + "/enrollment", input)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await unscoped.PostAsJsonAsync(url + "/overrides", change)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.GetStringAsync(url + "/enrollment")).ShouldBe(before);
        (await Client.GetStringAsync(url + "/overrides")).ShouldBe(overridesBefore);
    }

    [Fact]
    public async Task Esi_employee_actions_do_not_require_employee_profile_editing()
    {
        Guid employeeId;
        using (var scope = Factory.Services.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
        using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
        {
            var employee = await scope.ServiceProvider.GetRequiredService<IEmployeeRepository>()
                .GetEmployeeByUserIdAsync(Guid.Parse(TestAuthHandler.TestUserId));
            employeeId = employee!.Id;
        }
        var url = $"/api/payroll/statutory-schemes/esi/employees/{employeeId}";
        using var editor = Factory.CreateClient();
        editor.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
            ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.ManageEnrollment);
        var input = new UpdateEmployeeStatutoryEnrollmentDto { Status = "RegistrationPending", StatusReason = "Synthetic permission regression" };
        using var saved = await editor.PutAsJsonAsync(url + "/enrollment", input);
        saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        (await editor.GetAsync(url + "/enrollment")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var viewer = Factory.CreateClient();
        viewer.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
            ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.View);
        (await viewer.GetAsync(url + "/enrollment")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await viewer.PutAsJsonAsync(url + "/enrollment", input)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var approver = Factory.CreateClient();
        approver.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
            ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.ApproveOverride);
        var change = new CreateStatutoryOverrideDto { Reason = "Synthetic authorization test only", OverrideType = "ForceCovered",
            EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), EffectiveTo = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc) };
        using var approved = await approver.PostAsJsonAsync(url + "/overrides", change);
        approved.StatusCode.ShouldBe(HttpStatusCode.NoContent, await approved.Content.ReadAsStringAsync());
        (await editor.PostAsJsonAsync(url + "/overrides", change)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<PermissionGrantInfoDto[]> ReadAsync(string url)
    {
        using var response = await Client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<GetPermissionListResultDto>(JsonOptions);
        return result!.Groups.SelectMany(x => x.Permissions).ToArray();
    }
    private async Task SetAsync(string url, params (string Name, bool IsGranted)[] changes)
    {
        using var response = await Client.PutAsJsonAsync(url, new { permissions = changes.Select(x => new { name = x.Name, isGranted = x.IsGranted }) });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }
    private async Task<(string Role, Guid User)> CreateTargetAsync(Guid? tenantId = null)
    {
        using var scope = Factory.Services.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId ?? Guid.Parse(TestAuthHandler.TestTenantId));
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
        var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
        var name = "synthetic-permissions-" + Guid.NewGuid().ToString("N");
        var role = new IdentityRole(Guid.NewGuid(), name, tenantId ?? Guid.Parse(TestAuthHandler.TestTenantId));
        var user = new IdentityUser(Guid.NewGuid(), name, name + "@example.invalid", role.TenantId);
        user.AddRole(role.Id);
        db.Set<IdentityRole>().Add(role);
        db.Set<IdentityUser>().Add(user);
        await uow.CompleteAsync();
        return (name, user.Id);
    }
}
