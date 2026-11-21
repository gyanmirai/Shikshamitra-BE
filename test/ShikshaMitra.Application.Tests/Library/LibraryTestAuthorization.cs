using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Library;

internal static class LibraryTestAuthorization
{
    public static async Task GrantTenantWideAsync(IServiceProvider provider)
    {
        var userId = provider.GetRequiredService<ICurrentUser>().Id
            ?? throw new InvalidOperationException("The Library test requires an authenticated principal.");
        var tenantId = provider.GetRequiredService<ICurrentTenant>().Id;
        var users = provider.GetRequiredService<IIdentityUserRepository>();
        if (await users.FindAsync(userId) == null)
            await users.InsertAsync(new IdentityUser(userId, "library-test-user", "library-test@example.test", tenantId), autoSave: true);

        var scopes = provider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
        if (!await scopes.AnyAsync(x => x.TenantId == tenantId && x.UserId == userId && x.AcademicYearId == null))
            await scopes.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: userId,
                description: "Library workflow test authority"), autoSave: true);

        await provider.GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
    }
}
