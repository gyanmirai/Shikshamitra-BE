using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Onboarding;

public class StudentRollNumberBatchConfiguration : IEntityTypeConfiguration<StudentRollNumberBatch>
{
	public void Configure(EntityTypeBuilder<StudentRollNumberBatch> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentRollNumberBatches", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Operation).HasMaxLength(20).IsRequired();
		builder.Property(x => x.Status).HasMaxLength(24).IsRequired();
		builder.Property(x => x.RosterHash).HasMaxLength(64).IsRequired();
		builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
		builder.Property(x => x.SubmissionKey).HasMaxLength(100);
		foreach (var property in new[] { nameof(StudentRollNumberBatch.RuleJson), nameof(StudentRollNumberBatch.RowsJson), nameof(StudentRollNumberBatch.PolicyJson), nameof(StudentRollNumberBatch.ApprovalStagesJson), nameof(StudentRollNumberBatch.DecisionsJson) })
			builder.Property<string>(property).HasColumnType("jsonb").IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.DivisionId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.RequestedBy, x.SubmissionKey }).IsUnique().HasFilter("\"SubmissionKey\" IS NOT NULL AND \"IsDeleted\" = false");
	}
}
