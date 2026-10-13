using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionDocumentConfiguration : IEntityTypeConfiguration<AdmissionDocumentEvidence>, IEntityTypeConfiguration<AdmissionDocumentReview>
{
	public void Configure(EntityTypeBuilder<AdmissionDocumentEvidence> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionDocumentEvidence", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.ConfigureUtcInstants(nameof(AdmissionDocumentEvidence.SubmittedAt),
			nameof(AdmissionDocumentEvidence.OriginalSeenAt), nameof(AdmissionDocumentEvidence.StudentLinkedAt));
		builder.Property(item => item.IsDeleted).ValueGeneratedNever().HasDefaultValue(false);
		builder.Property(item => item.RequirementCode).HasMaxLength(64).IsRequired();
		builder.Property(item => item.RequirementLabel).HasMaxLength(128).IsRequired();
		builder.Property(item => item.RequirementKind).HasMaxLength(16).IsRequired();
		builder.Property(item => item.CollectionMode).HasMaxLength(16).IsRequired();
		builder.Property(item => item.Kind).HasMaxLength(16).IsRequired();
		builder.Property(item => item.FileName).HasMaxLength(255);
		builder.Property(item => item.ContentType).HasMaxLength(128);
		builder.Property(item => item.Sha256Hash).HasMaxLength(64);
		builder.Property(item => item.BlobName).HasMaxLength(512);
		builder.Property(item => item.Notes).HasMaxLength(1024);
		builder.Property(item => item.PolicySnapshotJson).HasColumnType("text").IsRequired();
		builder.Ignore(item => item.OriginalSeen);
		builder.HasIndex(item => new { item.StageInstanceId, item.RequirementCode, item.VersionNumber }).IsUnique().HasDatabaseName("UX_AdmissionDocuments_Stage_Requirement_Version");
		builder.HasIndex(item => new { item.TenantId, item.ApplicationId, item.StageInstanceId }).HasDatabaseName("IX_AdmissionDocuments_Application_Stage");
		builder.HasIndex(item => new { item.TenantId, item.StudentId }).HasDatabaseName("IX_AdmissionDocuments_Student");
		builder.HasOne<AdmissionApplication>().WithMany().HasForeignKey(item => item.ApplicationId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionApplicationStageInstance>().WithMany().HasForeignKey(item => item.StageInstanceId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<DocumentType>().WithMany().HasForeignKey(item => item.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<Student>().WithMany().HasForeignKey(item => item.StudentId).OnDelete(DeleteBehavior.Restrict);
	}
	public void Configure(EntityTypeBuilder<AdmissionDocumentReview> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionDocumentReviews", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.ConfigureUtcInstants(nameof(AdmissionDocumentReview.ReviewedAt));
		builder.Property(item => item.IsDeleted).ValueGeneratedNever().HasDefaultValue(false);
		builder.Property(item => item.Decision).HasMaxLength(16).IsRequired();
		builder.Property(item => item.Reason).HasMaxLength(1024);
		builder.HasIndex(item => item.EvidenceId).IsUnique().HasDatabaseName("UX_AdmissionDocumentReviews_Evidence");
		builder.HasIndex(item => new { item.TenantId, item.ApplicationId, item.StageInstanceId }).HasDatabaseName("IX_AdmissionDocumentReviews_Application_Stage");
		builder.HasOne<AdmissionDocumentEvidence>().WithMany().HasForeignKey(item => item.EvidenceId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionApplication>().WithMany().HasForeignKey(item => item.ApplicationId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionApplicationStageInstance>().WithMany().HasForeignKey(item => item.StageInstanceId).OnDelete(DeleteBehavior.Restrict);
	}
}
