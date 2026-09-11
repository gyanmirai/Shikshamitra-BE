using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionReviewConfiguration : IEntityTypeConfiguration<AdmissionReviewWorkItem>, IEntityTypeConfiguration<AdmissionReviewRecord>, IEntityTypeConfiguration<AdmissionReviewDecision>
{
	public void Configure(EntityTypeBuilder<AdmissionReviewWorkItem> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionReviewWorkItems", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(item => item.IsDeleted).ValueGeneratedNever().HasDefaultValue(false);
		builder.Property(item => item.Status).HasMaxLength(32).IsRequired();
		builder.HasIndex(item => item.StageInstanceId).IsUnique().HasDatabaseName("UX_AdmissionReviewWorkItem_Stage");
		builder.HasIndex(item => new { item.TenantId, item.AssignedReviewerId, item.DueAt }).HasDatabaseName("IX_AdmissionReviewWorkItem_Assigned_Due");
		builder.HasOne<AdmissionApplication>().WithMany().HasForeignKey(item => item.ApplicationId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionApplicationStageInstance>().WithMany().HasForeignKey(item => item.StageInstanceId).OnDelete(DeleteBehavior.Restrict);
	}
	public void Configure(EntityTypeBuilder<AdmissionReviewRecord> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionReviewRecords", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(item => item.IsDeleted).ValueGeneratedNever().HasDefaultValue(false);
		builder.Property(item => item.Status).HasMaxLength(16).IsRequired();
		builder.Property(item => item.Recommendation).HasMaxLength(32);
		builder.Property(item => item.Reason).HasMaxLength(1024);
		builder.Property(item => item.Notes).HasMaxLength(1024);
		builder.Property(item => item.InputFingerprint).HasMaxLength(64).IsRequired();
		builder.Property(item => item.PolicySnapshotJson).HasColumnType("text").IsRequired();
		builder.Property(item => item.ChecklistSnapshotJson).HasColumnType("text").IsRequired();
		builder.Property(item => item.EvidenceSnapshotJson).HasColumnType("text").IsRequired();
		builder.HasIndex(item => new { item.StageInstanceId, item.SequenceNumber }).IsUnique().HasDatabaseName("UX_AdmissionReviewRecord_Stage_Sequence");
		builder.HasIndex(item => new { item.TenantId, item.ApplicationId, item.StageInstanceId }).HasDatabaseName("IX_AdmissionReviewRecord_Application_Stage");
		builder.HasOne<AdmissionApplication>().WithMany().HasForeignKey(item => item.ApplicationId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionApplicationStageInstance>().WithMany().HasForeignKey(item => item.StageInstanceId).OnDelete(DeleteBehavior.Restrict);
	}
	public void Configure(EntityTypeBuilder<AdmissionReviewDecision> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionReviewDecisions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(item => item.IsDeleted).ValueGeneratedNever().HasDefaultValue(false);
		builder.Property(item => item.Decision).HasMaxLength(32).IsRequired();
		builder.Property(item => item.Reason).HasMaxLength(1024);
		builder.HasIndex(item => item.RecordId).IsUnique().HasDatabaseName("UX_AdmissionReviewDecision_Record");
		builder.HasIndex(item => new { item.TenantId, item.ApplicationId, item.StageInstanceId }).HasDatabaseName("IX_AdmissionReviewDecision_Application_Stage");
		builder.HasOne<AdmissionApplication>().WithMany().HasForeignKey(item => item.ApplicationId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionApplicationStageInstance>().WithMany().HasForeignKey(item => item.StageInstanceId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionReviewRecord>().WithMany().HasForeignKey(item => item.RecordId).OnDelete(DeleteBehavior.Restrict);
	}
}
