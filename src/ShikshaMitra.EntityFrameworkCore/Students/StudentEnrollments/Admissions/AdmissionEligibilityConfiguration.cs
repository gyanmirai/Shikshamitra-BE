using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionEligibilityConfiguration : IEntityTypeConfiguration<AdmissionEligibilityEvaluation>, IEntityTypeConfiguration<AdmissionEligibilityException>
{
	public void Configure(EntityTypeBuilder<AdmissionEligibilityEvaluation> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionEligibilityEvaluations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(item => item.IsDeleted).ValueGeneratedNever().HasDefaultValue(false);
		builder.Property(item => item.Outcome).HasMaxLength(32).IsRequired();
		builder.Property(item => item.InputFingerprint).HasMaxLength(64).IsRequired();
		builder.Property(item => item.PolicySnapshotJson).HasColumnType("text").IsRequired();
		builder.Property(item => item.FactsSnapshotJson).HasColumnType("text").IsRequired();
		builder.Property(item => item.CriteriaSnapshotJson).HasColumnType("text").IsRequired();
		builder.HasIndex(item => new { item.StageInstanceId, item.SequenceNumber }).IsUnique().HasDatabaseName("UX_AdmissionEligibility_Stage_Sequence");
		builder.HasIndex(item => new { item.TenantId, item.ApplicationId, item.StageInstanceId }).HasDatabaseName("IX_AdmissionEligibility_Application_Stage");
		builder.HasOne<AdmissionApplication>().WithMany().HasForeignKey(item => item.ApplicationId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionApplicationStageInstance>().WithMany().HasForeignKey(item => item.StageInstanceId).OnDelete(DeleteBehavior.Restrict);
	}
	public void Configure(EntityTypeBuilder<AdmissionEligibilityException> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionEligibilityExceptions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(item => item.IsDeleted).ValueGeneratedNever().HasDefaultValue(false);
		builder.Property(item => item.Decision).HasMaxLength(16).IsRequired();
		builder.Property(item => item.Reason).HasMaxLength(1024).IsRequired();
		builder.HasIndex(item => item.RequestId).IsUnique().HasDatabaseName("UX_AdmissionEligibilityException_Request");
		builder.HasIndex(item => new { item.TenantId, item.ApplicationId, item.StageInstanceId }).HasDatabaseName("IX_AdmissionEligibilityException_Application_Stage");
		builder.HasOne<AdmissionEligibilityEvaluation>().WithMany().HasForeignKey(item => item.EvaluationId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionEligibilityException>().WithMany().HasForeignKey(item => item.RequestId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionApplication>().WithMany().HasForeignKey(item => item.ApplicationId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionApplicationStageInstance>().WithMany().HasForeignKey(item => item.StageInstanceId).OnDelete(DeleteBehavior.Restrict);
	}
}
