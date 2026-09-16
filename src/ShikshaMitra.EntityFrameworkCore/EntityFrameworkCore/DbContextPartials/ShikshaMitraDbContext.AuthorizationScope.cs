using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
    private IDisposable? InvalidateChangedAuthorizationScope()
    {
        // ABP entity events can wait until UOW completion. Invalidate at persistence too,
        // including direct context writes, role join changes, imports, and deletions.
        if (LazyServiceProvider == null || !ChangeTracker.Entries().Any(entry =>
            entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
            entry.Entity is UserScopeAssignment or Employee or IdentityUser or IdentityRole or IdentityUserRole))
            return null;
        var manager = LazyServiceProvider.LazyGetRequiredService<IUnitOfWorkManager>();
        ScopeLocalReuseInvalidator.ClearCurrentUnitOfWork(manager);
        return new AuthorizationScopeChange(manager);
    }

    private sealed class AuthorizationScopeChange(IUnitOfWorkManager manager) : IDisposable
    {
        public void Dispose() => ScopeLocalReuseInvalidator.ClearCurrentUnitOfWork(manager);
    }
}
