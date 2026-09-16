using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.HomeworkAssignment;

/// <summary>
/// EF Core entity configuration for AssignmentEntity
/// </summary>
public class AssignmentEntityConfiguration : IEntityTypeConfiguration<AssignmentEntity>
{
	public void Configure(EntityTypeBuilder<AssignmentEntity> builder)
	{
		// Table configuration
		builder.ToTable(
			ShikshaMitraConsts.AcademicDbTablePrefix + "Assignments",
			ShikshaMitraConsts.DbSchema
		);

		// ABP conventions (handles audit fields, multi-tenancy, etc.)
		builder.ConfigureByConvention();

		// Properties
		builder.Property(x => x.StandardId).IsRequired();
		builder.Property(x => x.DivisionId).IsRequired(); // For compatibility
		builder.Property(x => x.SubjectId).IsRequired();
		builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Description).IsRequired();
		builder.Property(x => x.AssignmentTypeId).IsRequired();
		builder.Property(x => x.SubmissionType).IsRequired();
			builder.Property(x => x.TotalMarks).IsRequired().HasColumnType("decimal(10,2)");
			builder.Property(x => x.EvaluationRequired).IsRequired(false);
			builder.Property(x => x.EvaluationMethod).IsRequired(false);
			builder.Property(x => x.Status).IsRequired().HasDefaultValue(AssignmentStatus.Draft);

		builder.Property(x => x.AttachmentsJson).IsRequired(false);

		// Indexes for performance
		builder
			.HasIndex(x => x.StandardId)
			.HasDatabaseName("IX_Assignments_StandardId");

		builder
			.HasIndex(x => x.SubjectId)
			.HasDatabaseName("IX_Assignments_SubjectId");

		builder
			.HasIndex(x => x.AssignmentTypeId)
			.HasDatabaseName("IX_Assignments_AssignmentTypeId");

		builder
			.HasIndex(x => x.TenantId)
			.HasDatabaseName("IX_Assignments_TenantId");

		builder
			.HasIndex(x => x.AcademicYearId)
			.HasDatabaseName("IX_Assignments_AcademicYearId");

		// Composite index for common queries (TenantId + StandardId + SubjectId)
		builder
			.HasIndex(x => new { x.TenantId, x.StandardId, x.SubjectId })
			.HasDatabaseName("IX_Assignments_Tenant_Standard_Subject");

		// Foreign Keys
		builder
			.HasOne(x => x.AssignmentType)
			.WithMany()
			.HasForeignKey(x => x.AssignmentTypeId)
			.OnDelete(DeleteBehavior.Restrict);

		// Configure one-to-many relationship with AssignmentDivisionSchedule
		builder
			.HasMany(x => x.DivisionSchedules)
			.WithOne(s => s.Assignment)
			.HasForeignKey(s => s.AssignmentId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
