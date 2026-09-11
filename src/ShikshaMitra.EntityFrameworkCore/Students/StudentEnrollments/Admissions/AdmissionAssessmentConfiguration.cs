using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentConfiguration : IEntityTypeConfiguration<AdmissionAssessmentSession>, IEntityTypeConfiguration<AdmissionAssessmentAttempt>
{
	public void Configure(EntityTypeBuilder<AdmissionAssessmentSession> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionAssessmentSessions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(item => item.IsDeleted).ValueGeneratedNever().HasDefaultValue(false);
		builder.Property(item => item.Name).HasMaxLength(128).IsRequired();
		builder.Property(item => item.Location).HasMaxLength(256).IsRequired();
		builder.HasIndex(item => new { item.TenantId, item.AdmissionOfferingId, item.WorkflowStageDefinitionId, item.StartsAt }).HasDatabaseName("IX_AssessmentSessions_Offering_Stage_Start");
		builder.HasOne<AdmissionOffering>().WithMany().HasForeignKey(item => item.AdmissionOfferingId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionWorkflowStageDefinition>().WithMany().HasForeignKey(item => item.WorkflowStageDefinitionId).OnDelete(DeleteBehavior.Restrict);
	}
	public void Configure(EntityTypeBuilder<AdmissionAssessmentAttempt> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionAssessmentAttempts", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(item => item.IsDeleted).ValueGeneratedNever().HasDefaultValue(false);
		builder.Property(item => item.Attendance).HasMaxLength(16).IsRequired();
		builder.Property(item => item.Status).HasMaxLength(24).IsRequired();
		builder.Property(item => item.Outcome).HasMaxLength(16).IsRequired();
		builder.Property(item => item.Notes).HasMaxLength(1024);
		builder.Property(item => item.ReviewNotes).HasMaxLength(1024);
		builder.Property(item => item.PolicySnapshotJson).HasColumnType("text").IsRequired();
		builder.Property(item => item.Marks).HasColumnType("numeric");
		builder.HasIndex(item => new { item.StageInstanceId, item.AttemptNumber }).IsUnique().HasDatabaseName("UX_AssessmentAttempts_Stage_Number");
		builder.HasIndex(item => new { item.TenantId, item.ApplicationId, item.StageInstanceId }).HasDatabaseName("IX_AssessmentAttempts_Application_Stage");
		builder.HasOne<AdmissionApplication>().WithMany().HasForeignKey(item => item.ApplicationId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionApplicationStageInstance>().WithMany().HasForeignKey(item => item.StageInstanceId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionAssessmentSession>().WithMany().HasForeignKey(item => item.SessionId).OnDelete(DeleteBehavior.Restrict);
	}
}
