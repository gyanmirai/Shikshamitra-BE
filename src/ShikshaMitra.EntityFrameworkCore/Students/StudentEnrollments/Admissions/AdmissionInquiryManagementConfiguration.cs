using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryManagementConfiguration :
	IEntityTypeConfiguration<AdmissionInquiryManagement>,
	IEntityTypeConfiguration<AdmissionInquiryFollowUp>
{
	public void Configure(EntityTypeBuilder<AdmissionInquiryManagement> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionInquiryManagement", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(item => item.IsDeleted).ValueGeneratedNever().HasDefaultValue(false);
		builder.Property(item => item.PolicySnapshotJson).HasColumnType("text").IsRequired();
		builder.Property(item => item.QualificationAnswersJson).HasColumnType("text");
		builder.Property(item => item.OutcomeReason).HasMaxLength(1024);
		builder.HasIndex(item => item.InquiryId).IsUnique().HasDatabaseName("UX_AdmissionInquiryManagement_Inquiry");
		builder.HasIndex(item => new { item.TenantId, item.FirstContactDueAt }).HasDatabaseName("IX_AdmissionInquiryManagement_FirstContactDue");
		builder.HasIndex(item => new { item.TenantId, item.QualificationDueAt }).HasDatabaseName("IX_AdmissionInquiryManagement_QualificationDue");
		builder.HasIndex(item => new { item.TenantId, item.Outcome }).HasDatabaseName("IX_AdmissionInquiryManagement_Outcome");
		builder.HasOne<AdmissionInquiry>().WithMany().HasForeignKey(item => item.InquiryId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionWorkflowStageDefinition>().WithMany().HasForeignKey(item => item.StageDefinitionId).OnDelete(DeleteBehavior.Restrict);
	}

	public void Configure(EntityTypeBuilder<AdmissionInquiryFollowUp> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionInquiryFollowUps", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(item => item.IsDeleted).ValueGeneratedNever().HasDefaultValue(false);
		builder.Property(item => item.Purpose).HasMaxLength(512).IsRequired();
		builder.Property(item => item.CompletionNote).HasMaxLength(1024);
		builder.HasIndex(item => new { item.TenantId, item.InquiryId, item.Status, item.ScheduledAt }).HasDatabaseName("IX_AdmissionInquiryFollowUp_Inquiry_Status_Due");
		builder.HasIndex(item => new { item.TenantId, item.AssignedCounsellorId, item.Status, item.ScheduledAt }).HasDatabaseName("IX_AdmissionInquiryFollowUp_Assigned_Status_Due");
		builder.HasOne<AdmissionInquiry>().WithMany().HasForeignKey(item => item.InquiryId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionInquiryManagement>().WithMany().HasForeignKey(item => item.ManagementId).OnDelete(DeleteBehavior.Restrict);
	}
}
