using Microsoft.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.SchoolIdentity;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<SchoolIdentityProfile> SchoolIdentityProfiles { get; set; }
	public DbSet<SchoolIdentityAsset> SchoolIdentityAssets { get; set; }
	public DbSet<SchoolIdentityRevision> SchoolIdentityRevisions { get; set; }
}
