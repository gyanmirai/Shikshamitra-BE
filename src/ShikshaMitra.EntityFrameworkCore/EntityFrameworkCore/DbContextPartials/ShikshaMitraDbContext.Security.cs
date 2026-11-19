using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Security.Permissions;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
    // ───────────────────────────────────────────────
    // Security — UserScope
    // ───────────────────────────────────────────────
    public DbSet<UserPermissionDeny> UserPermissionDenies { get; set; }
    public DbSet<UserScopeAssignment> UserScopeAssignments { get; set; }
}
