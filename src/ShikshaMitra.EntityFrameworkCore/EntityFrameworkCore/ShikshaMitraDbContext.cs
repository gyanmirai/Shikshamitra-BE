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
using Volo.Abp.EntityFrameworkCore.Modeling;
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
		builder.Ignore<FeeRefundReservation>();
		builder.Ignore<FeeRefundSettlement>();

		builder.ApplyConfigurationsFromAssembly(typeof(ShikshaMitraDbContext).Assembly);
		builder.Entity<TeacherDiary>(b =>
		{
			b.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "TeacherDiaries", ShikshaMitraConsts.DbSchema);
			b.ConfigureByConvention();
			b.Property(x => x.ActualTopicCovered).HasMaxLength(256);
			b.Property(x => x.ActualContentSummary).HasMaxLength(2048);
			b.Property(x => x.TeachingMethodUsed).HasMaxLength(256);
			b.Property(x => x.StudentResponse).HasMaxLength(1024);
			b.Property(x => x.ClassUnderstandingLevel).HasMaxLength(64);
			b.Property(x => x.StudentEngagementLevel).HasMaxLength(64);
			b.Property(x => x.Reflection).HasMaxLength(2048);
			b.Property(x => x.WhatWentWell).HasMaxLength(1024);
			b.Property(x => x.WhatWasDifficult).HasMaxLength(1024);
			b.Property(x => x.StudentsNeedingSupport).HasMaxLength(1024);
			b.Property(x => x.NextClassFollowUp).HasMaxLength(1024);
			b.Property(x => x.RemedialActionRequired).HasMaxLength(1024);
			b.Property(x => x.HomeworkAssigned).HasMaxLength(1024);
			b.Property(x => x.MissedReason).HasMaxLength(128);
			b.Property(x => x.BackdatedReason).HasMaxLength(1024);
			b.Property(x => x.TeacherRemarks).HasMaxLength(2048);
			b.Property(x => x.Status).HasConversion<int>().IsRequired();
			b.HasIndex(x => x.LessonPlanId).IsUnique();
			b.HasIndex(x => new { x.TeacherId, x.ActualDate });
		});
		builder.ConfigureBlobStoring();
	}
}
