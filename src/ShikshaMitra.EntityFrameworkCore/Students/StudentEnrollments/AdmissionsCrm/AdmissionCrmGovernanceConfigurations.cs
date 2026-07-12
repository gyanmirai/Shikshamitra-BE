using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRetentionPolicyConfiguration : IEntityTypeConfiguration<AdmissionCrmRetentionPolicy>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmRetentionPolicy> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmRetentionPolicies", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Code).HasMaxLength(AdmissionCrmConsts.MaxPolicyCodeLength).IsRequired();
		builder.Property(x => x.Name).HasMaxLength(AdmissionCrmConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.ScopeJson).HasColumnType("jsonb").IsRequired();
		builder.Property(x => x.AuthorityNote).HasMaxLength(AdmissionCrmConsts.MaxNotesLength).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Code }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status, x.Category });
	}
}

public class AdmissionCrmPrivacyRequestConfiguration : IEntityTypeConfiguration<AdmissionCrmPrivacyRequest>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmPrivacyRequest> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmPrivacyRequests", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Reason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength).IsRequired();
		builder.Property(x => x.PreviewJson).HasColumnType("jsonb").IsRequired();
		builder.Property(x => x.DecisionReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.CompletionReference).HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.LeadId, x.RequestedAt });
	}
}

public class AdmissionCrmExportRequestConfiguration : IEntityTypeConfiguration<AdmissionCrmExportRequest>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmExportRequest> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmExportRequests", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.DefinitionVersion).HasMaxLength(20).IsRequired();
		builder.Property(x => x.FiltersJson).HasColumnType("jsonb").IsRequired();
		builder.Property(x => x.FileName).HasMaxLength(AdmissionCrmConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
		builder.Property(x => x.Content).HasColumnType("text").IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.ReportType, x.RequestedAt });
		builder.HasIndex(x => new { x.TenantId, x.Status, x.ExpiresAt });
	}
}

public class AdmissionCrmReportScheduleConfiguration : IEntityTypeConfiguration<AdmissionCrmReportSchedule>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmReportSchedule> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmReportSchedules", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Name).HasMaxLength(AdmissionCrmConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.Destination).HasMaxLength(AdmissionCrmConsts.MaxNotesLength).IsRequired();
		builder.Property(x => x.FiltersJson).HasColumnType("jsonb").IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IsActive, x.NextRunAt });
	}
}

public class AdmissionCrmWorkDelegationConfiguration : IEntityTypeConfiguration<AdmissionCrmWorkDelegation>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmWorkDelegation> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmWorkDelegations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.QueueTypesJson).HasColumnType("jsonb").IsRequired();
		builder.Property(x => x.Reason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.DelegateUserId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.DelegatorUserId, x.StartsAt, x.EndsAt });
	}
}

public class AdmissionCrmOperationsSavedViewConfiguration : IEntityTypeConfiguration<AdmissionCrmOperationsSavedView>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmOperationsSavedView> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmOperationsSavedViews", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Name).HasMaxLength(AdmissionCrmConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.FiltersJson).HasColumnType("jsonb").IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.OwnerUserId, x.Name }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IsShared });
	}
}
