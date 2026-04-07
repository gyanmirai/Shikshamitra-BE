using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
    // ───────────────────────────────────────────────
    // Security — UserScope
    // ───────────────────────────────────────────────
    public DbSet<UserScopeAssignment> UserScopeAssignments { get; set; }
}
