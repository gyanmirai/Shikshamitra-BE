using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmSettingsAccessPolicyConfiguration : IEntityTypeConfiguration<AdmissionCrmSettingsAccessPolicy>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmSettingsAccessPolicy> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmSettingsAccessPolicies", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.SettingsKey).HasMaxLength(64).IsRequired();
		builder.Property(x => x.SectionKey).HasMaxLength(64);
		builder.Property(x => x.AccessMode).HasConversion<int>();
		builder.HasIndex(x => new { x.TenantId, x.SettingsKey, x.SectionKey }).IsUnique().HasFilter("\"IsDeleted\" = false");
	}
}

public class AdmissionCrmPlatformSupportSessionConfiguration : IEntityTypeConfiguration<AdmissionCrmPlatformSupportSession>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmPlatformSupportSession> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmPlatformSupportSessions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
		builder.Property(x => x.Reference).HasMaxLength(100);
		builder.HasIndex(x => new { x.OperatorUserId, x.TargetTenantId, x.ExpiresAt });
	}
}

public class AdmissionCrmPlatformPresetConfiguration : IEntityTypeConfiguration<AdmissionCrmPlatformPreset>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmPlatformPreset> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmPlatformPresets", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
		builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
		builder.Property(x => x.ConfigurationJson).HasColumnType("jsonb").IsRequired();
		builder.Property(x => x.AccessPolicyJson).HasColumnType("jsonb").IsRequired();
		builder.Property(x => x.ReleaseNotes).HasMaxLength(2000);
		builder.Property(x => x.Status).HasConversion<int>();
		builder.HasIndex(x => new { x.Code, x.Version }).IsUnique().HasFilter("\"IsDeleted\" = false");
	}
}

public class AdmissionCrmPresetAdoptionConfiguration : IEntityTypeConfiguration<AdmissionCrmPresetAdoption>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmPresetAdoption> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmPresetAdoptions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.SnapshotHash).HasMaxLength(64).IsRequired();
		builder.Property(x => x.PreviousConfigurationJson).HasColumnType("jsonb").IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.AppliedAt });
	}
}
