using Microsoft.EntityFrameworkCore;
using ShikshaMitra.HostTenants;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<TenantStatusChange> TenantStatusChanges { get; set; }
}
