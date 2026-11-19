using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Seeders;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Security.Permissions;

public sealed class SecurityIntegrationPostgresFactAttribute : FactAttribute
{
    public SecurityIntegrationPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SECURITY_INTEGRATION_POSTGRES")))
            Skip = "Requires its own empty disposable loopback payroll_tests_* database in SECURITY_INTEGRATION_POSTGRES.";
    }
}

public class PermissionConsistencyPostgres_Tests
{
    [SecurityIntegrationPostgresFact]
    public async Task PostgreSQL_round_trips_and_seed_upgrades_preserve_revocations_and_manual_scope()
    {
        using var factory = new SecurityPostgresTestFactory("SECURITY_INTEGRATION_POSTGRES");
        var api = new PermissionConsistencyApi_Tests(factory);
        await api.Role_and_user_round_trips_preserve_independent_and_inherited_grants();
        await api.Invalid_batches_are_atomic_and_never_guess_aliases("Unknown.Future.Permission");
        await api.Duplicate_entries_and_ineligible_host_permissions_are_rejected_before_writing();
        await api.Both_read_routes_deny_unauthenticated_and_unauthorized_management_but_allow_self_reads();
        await api.Cross_tenant_provider_keys_are_not_readable_or_writable();
        await api.Unproxied_application_calls_cannot_bypass_esi_authorization();
        await api.Esi_configuration_requires_both_mutation_permissions("HR.PayrollManagement.Settings.StatutorySchemes.Edit");
        await api.Esi_configuration_requires_both_mutation_permissions("HR.PayrollManagement.Settings.StatutorySchemes.Activate");
        await api.Esi_employee_action_grants_cannot_bypass_scope_or_cross_tenant_boundaries();
        await api.Esi_employee_actions_do_not_require_employee_profile_editing();

        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
        var userId = Guid.NewGuid();
        var manualScopeId = Guid.NewGuid();
        const string permission = "HR.PayrollManagement.RunPayroll.Lock";
        using (var services = factory.Services.CreateScope())
        using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
        using (var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            var db = await services.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
            (await db.Set<PermissionGrant>().AnyAsync(x => x.ProviderName == "R" && x.ProviderKey == "HR Manager" && x.Name == permission)).ShouldBeTrue();
            foreach (var officeRole in new[] { "Clerk", "Office Assistant" })
            {
                var hrGrants = await db.Set<PermissionGrant>().Where(x => x.ProviderName == "R" &&
                    x.ProviderKey == officeRole && (x.Name == "HR" || x.Name.StartsWith("HR.")))
                    .Select(x => x.Name).OrderBy(x => x).ToArrayAsync();
                hrGrants.ShouldBe(new[] { "HR", "HR.LeaveManagement.LeaveCenter.Add",
                    "HR.LeaveManagement.LeaveCenter.Delete", "HR.LeaveManagement.LeaveCenter.View" });
            }
            await services.ServiceProvider.GetRequiredService<IPermissionManager>().SetAsync(permission, "R", "HR Manager", false);
            var user = new IdentityUser(userId, "synthetic-scope-revocation", "scope-revocation@example.invalid", tenantId);
            db.Set<IdentityUser>().Add(user);
            db.Set<IdentityRole>().Add(new IdentityRole(Guid.NewGuid(), "Synthetic custom role", tenantId));
            db.Set<UserScopeAssignment>().Add(new UserScopeAssignment(manualScopeId, tenantId, userId: userId, description: "Manual synthetic access approval"));
            // A dormant manual HR scope must not be reactivated by the ESI demo seeder.
            db.Set<UserScopeAssignment>().Add(new UserScopeAssignment(Guid.NewGuid(), tenantId, roleName: "HR Manager",
                description: "Manual synthetic HR restriction").Deactivate());
            // Force a missing-marker upgrade path without changing any other dataset.
            db.Set<SeedExecutionMarker>().RemoveRange(await db.Set<SeedExecutionMarker>()
                .Where(x => x.SeederName == "RolesSeeder" || x.SeederName == "RolePermissionSeeder").ToListAsync());
            await uow.CompleteAsync();
        }

        string[] beforeGrants = [];
        await AssertExistingIdentityIsNotRepairedAsync(factory, tenantId);
        using (var services = factory.Services.CreateScope())
        using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
        using (var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            var db = await services.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
            beforeGrants = await db.Set<PermissionGrant>().OrderBy(x => x.Id).Select(x => x.Id + ":" + x.Name).ToArrayAsync();
            (await services.ServiceProvider.GetRequiredService<IUserScopeService>().GetUserScopeAsync(userId)).HasTenantWideScope().ShouldBeTrue();
            foreach (var profile in new[] { "FullDemo", "Minimal", "FastDemo" })
            {
                var context = new DataSeedContext(tenantId).WithProperty("Profile", profile).WithProperty("Version", "security-upgrade-test");
                await ActivatorUtilities.CreateInstance<RolesSeederContributor>(services.ServiceProvider, uow).SeedAsync(context);
                await ActivatorUtilities.CreateInstance<RolePermissionSeederContributor>(services.ServiceProvider, uow).SeedAsync(context);
            }
            await ActivatorUtilities.CreateInstance<EsiFullDemoConfigurationSeederContributor>(services.ServiceProvider, uow).SeedAsync(new DataSeedContext(tenantId));
            await uow.CompleteAsync();
        }
        using (var services = factory.Services.CreateScope())
        using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
        using (var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            var db = await services.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
            (await db.Set<PermissionGrant>().OrderBy(x => x.Id).Select(x => x.Id + ":" + x.Name).ToArrayAsync()).ShouldBe(beforeGrants);
            (await db.Set<IdentityRole>().AnyAsync(x => x.Name == "Synthetic custom role")).ShouldBeTrue();
            (await db.Set<UserScopeAssignment>().SingleAsync(x => x.Id == manualScopeId)).IsActive.ShouldBeTrue();
            (await db.Set<UserScopeAssignment>().SingleAsync(x => x.Description == "Manual synthetic HR restriction")).IsActive.ShouldBeFalse();
            var scopeService = services.ServiceProvider.GetRequiredService<IUserScopeService>();
            (await scopeService.GetUserScopeAsync(userId)).HasTenantWideScope().ShouldBeTrue();
            var assignment = await db.Set<UserScopeAssignment>().SingleAsync(x => x.Id == manualScopeId);
            assignment.Deactivate();
            await uow.SaveChangesAsync();
            // Persisted entity events must invalidate the same unit of work, not only the next request.
            (await scopeService.GetUserScopeAsync(userId)).HasAnyAccess.ShouldBeFalse();
            await uow.CompleteAsync();
        }
        using (var services = factory.Services.CreateScope())
        using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
        using (var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
        {
            (await services.ServiceProvider.GetRequiredService<IUserScopeService>().GetUserScopeAsync(userId)).HasAnyAccess.ShouldBeFalse();
        }
    }

    private static async Task AssertExistingIdentityIsNotRepairedAsync(SecurityPostgresTestFactory factory, Guid tenantId)
    {
        using var services = factory.Services.CreateScope();
        using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
        var users = services.ServiceProvider.GetRequiredService<IIdentityUserRepository>();
        var manager = services.ServiceProvider.GetRequiredService<IdentityUserManager>();
        var expected = new System.Collections.Generic.Dictionary<Guid, string?>();
        foreach (var name in new[] { "admin-ddvs", "hr-manager" })
        {
            var user = await users.FindByNormalizedUserNameAsync(name.ToUpperInvariant());
            user.ShouldNotBeNull();
            (await manager.RemovePasswordAsync(user)).Succeeded.ShouldBeTrue();
            (await manager.AddPasswordAsync(user, "Synthetic-Changed-Only-2026!")).Succeeded.ShouldBeTrue();
            (await manager.SetRolesAsync(user, new[] { "Synthetic custom role" })).Succeeded.ShouldBeTrue();
            expected[user.Id] = user.PasswordHash;
        }
        await uow.SaveChangesAsync();
        var context = new DataSeedContext(tenantId).WithProperty("SeedTestLogins", true);
        await ActivatorUtilities.CreateInstance<UserSeederContributor>(services.ServiceProvider, uow).SeedAsync(context);
        await ActivatorUtilities.CreateInstance<HRSeederContributor>(services.ServiceProvider, uow).SeedAsync(context);
        await ActivatorUtilities.CreateInstance<TestLoginSeederContributor>(services.ServiceProvider, uow).SeedAsync(context);
        await uow.SaveChangesAsync();
        foreach (var (id, hash) in expected)
        {
            var user = await users.GetAsync(id);
            user.PasswordHash.ShouldBe(hash);
            (await manager.GetRolesAsync(user)).ShouldBe(new[] { "Synthetic custom role" });
        }
        await uow.CompleteAsync();
    }
}
