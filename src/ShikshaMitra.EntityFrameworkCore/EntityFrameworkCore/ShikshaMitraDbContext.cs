using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.HR.Employees;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore;

/// <summary>
/// Main DbContext. Module-specific DbSet properties are defined in partial class files
/// under EntityFrameworkCore/DbContextPartials/.
/// </summary>
[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ConnectionStringName("Default")]
public partial class ShikshaMitraDbContext(DbContextOptions<ShikshaMitraDbContext> options)
	: AbpDbContext<ShikshaMitraDbContext>(options),
		IIdentityDbContext,
		ITenantManagementDbContext
{
	// ───────────────────────────────────────────────
	// ABP Identity
	// ───────────────────────────────────────────────
	public DbSet<IdentityUser> Users { get; set; }
	public DbSet<IdentityRole> Roles { get; set; }
	public DbSet<IdentityClaimType> ClaimTypes { get; set; }
	public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
	public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
	public DbSet<IdentityLinkUser> LinkUsers { get; set; }
	public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
	public DbSet<IdentitySession> Sessions { get; set; }

	// ───────────────────────────────────────────────
	// ABP Tenant Management
	// ───────────────────────────────────────────────
	public DbSet<Tenant> Tenants { get; set; }
	public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

	// ───────────────────────────────────────────────
	// OnModelCreating
	// ───────────────────────────────────────────────
	protected override void OnModelCreating(ModelBuilder builder)
	{
		base.OnModelCreating(builder);

		// ABP module configurations
		builder.ConfigurePermissionManagement();
		builder.ConfigureSettingManagement();
		builder.ConfigureBackgroundJobs();
		builder.ConfigureAuditLogging();
		builder.ConfigureIdentity();
		builder.ConfigureOpenIddict();
		builder.ConfigureFeatureManagement();
		builder.ConfigureTenantManagement();

		// Ignore owned types before ApplyConfigurations so they are not also discovered as root entities.
		builder.Ignore<EmployeeDocument>();
		builder.Ignore<AllocatedTeacher>();
		builder.Ignore<FeeRefundLine>();

		builder.ApplyConfigurationsFromAssembly(typeof(ShikshaMitraDbContext).Assembly);
		builder.ConfigureBlobStoring();
	}
}
