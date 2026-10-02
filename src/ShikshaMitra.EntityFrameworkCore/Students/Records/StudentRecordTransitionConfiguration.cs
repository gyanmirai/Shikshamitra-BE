using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.Records;

public class StudentRecordTransitionConfiguration : IEntityTypeConfiguration<StudentRecordTransition>,
	IEntityTypeConfiguration<StudentRecordTransitionBatch>, IEntityTypeConfiguration<StudentRecordTransitionEvent>, IEntityTypeConfiguration<StudentRecordRenewalCase>
{
	public void Configure(EntityTypeBuilder<StudentRecordTransition> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentRecordTransitions", ShikshaMitraConsts.DbSchema); builder.ConfigureByConvention();
		builder.Ignore(x => x.ReturnHandoffId);
		builder.Property(x => x.Outcome).IsRequired().HasMaxLength(30); builder.Property(x => x.Status).IsRequired().HasMaxLength(30);
		builder.Property(x => x.Reason).IsRequired().HasMaxLength(1000); builder.Property(x => x.LastError).HasMaxLength(200);
		builder.Property(x => x.InputJson).IsRequired().HasColumnType("text"); builder.Property(x => x.BeforeJson).IsRequired().HasColumnType("text");
		builder.Property(x => x.AfterJson).HasColumnType("text"); builder.Property(x => x.PolicyJson).IsRequired().HasColumnType("text"); builder.Property(x => x.ApprovalRuleJson).IsRequired().HasColumnType("text");
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.AcademicYearId, x.CreationTime });
		builder.HasIndex(x => new { x.TenantId, x.Status, x.EffectiveDate });
		builder.HasIndex(x => new { x.TenantId, x.TargetAcademicYearId, x.TargetStandardId, x.TargetDivisionId });
		builder.HasIndex(x => new { x.TenantId, x.BatchId, x.StudentId }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.CorrectionOfId }).IsUnique().HasFilter("\"CorrectionOfId\" IS NOT NULL AND \"IsDeleted\" = false AND \"Status\" NOT IN ('Rejected','Cancelled')");
		builder.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<StudentEnrollment>().WithMany().HasForeignKey(x => x.SourceEnrollmentId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<StudentRecordTransitionBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
	}
	public void Configure(EntityTypeBuilder<StudentRecordTransitionBatch> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentRecordTransitionBatches", ShikshaMitraConsts.DbSchema); builder.ConfigureByConvention();
		builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128); builder.Property(x => x.RequestJson).IsRequired().HasColumnType("text"); builder.Property(x => x.ExceptionRowsJson).HasColumnType("text");
		builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
	}
	public void Configure(EntityTypeBuilder<StudentRecordTransitionEvent> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentRecordTransitionEvents", ShikshaMitraConsts.DbSchema); builder.ConfigureByConvention();
		builder.Property(x => x.Action).IsRequired().HasMaxLength(40); builder.Property(x => x.Remarks).HasMaxLength(1000);
		builder.HasIndex(x => new { x.TenantId, x.TransitionId, x.CreationTime });
		builder.HasOne<StudentRecordTransition>().WithMany().HasForeignKey(x => x.TransitionId).OnDelete(DeleteBehavior.Restrict);
	}
	public void Configure(EntityTypeBuilder<StudentRecordRenewalCase> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentRecordRenewalCases", ShikshaMitraConsts.DbSchema); builder.ConfigureByConvention();
		builder.Property(x => x.Status).IsRequired().HasMaxLength(30); builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
		builder.Property(x => x.PolicyJson).IsRequired().HasColumnType("text"); builder.Property(x => x.OnboardingRulesJson).IsRequired().HasColumnType("text");
		builder.Property(x => x.SourceSnapshotJson).IsRequired().HasColumnType("text"); builder.Property(x => x.ReviewRemarks).HasMaxLength(1000);
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.AcademicYearId }).IsUnique().HasFilter("\"IsDeleted\" = false AND \"Status\" NOT IN ('Completed','Cancelled')");
		builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
	}
}
