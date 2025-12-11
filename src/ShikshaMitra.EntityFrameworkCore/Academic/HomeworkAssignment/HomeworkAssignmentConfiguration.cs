using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class HomeworkAssignmentConfiguration : IEntityTypeConfiguration<HomeworkAssignmentEntity>
{
	public void Configure(EntityTypeBuilder<HomeworkAssignmentEntity> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "HomeworkAssignments",
			ShikshaMitraConsts.DbSchema
		);
		builder.ConfigureByConvention();

		// Properties
		builder.Property(x => x.StandardId).IsRequired();
		builder.Property(x => x.SubjectId).IsRequired();
		builder.Property(x => x.Title).IsRequired().HasMaxLength(500);
		builder.Property(x => x.Description).IsRequired();
		builder.Property(x => x.SubmissionType).IsRequired();

		// V1 fields - made optional for V2 homework support
		builder.Property(x => x.HomeworkAssignmentTypeId).IsRequired(false);
		builder.Property(x => x.DivisionId).IsRequired(false);
		builder.Property(x => x.DueDate).IsRequired(false);
		builder.Property(x => x.IsPublished).IsRequired().HasDefaultValue(false);

		// V2 fields - optional
		builder.Property(x => x.HomeworkTypeId).IsRequired(false);
		builder.Property(x => x.ChapterIdsJson).IsRequired(false);
		builder.Property(x => x.AttachmentsJson).IsRequired(false);

		// Foreign Keys
		builder
			.HasOne(x => x.HomeworkAssignmentType)
			.WithMany()
			.HasForeignKey(x => x.HomeworkAssignmentTypeId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired(false);

		builder
			.HasOne(x => x.HomeworkType)
			.WithMany()
			.HasForeignKey(x => x.HomeworkTypeId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired(false);

		// Configure one-to-many relationship with HomeworkDivisionSchedule
		builder
			.HasMany(x => x.DivisionSchedules)
			.WithOne(s => s.Homework)
			.HasForeignKey(s => s.HomeworkId)
			.OnDelete(DeleteBehavior.Cascade);

		// Indexes
		builder
			.HasIndex(x => x.StandardId)
			.HasDatabaseName("IX_HomeworkAssignments_StandardId");

		builder
			.HasIndex(x => x.SubjectId)
			.HasDatabaseName("IX_HomeworkAssignments_SubjectId");

		builder
			.HasIndex(x => x.HomeworkAssignmentTypeId)
			.HasDatabaseName("IX_HomeworkAssignments_HomeworkAssignmentTypeId");

		builder
			.HasIndex(x => x.HomeworkTypeId)
			.HasDatabaseName("IX_HomeworkAssignments_HomeworkTypeId");

		builder
			.HasIndex(x => x.TenantId)
			.HasDatabaseName("IX_HomeworkAssignments_TenantId");

		builder
			.HasIndex(x => x.AcademicYearId)
			.HasDatabaseName("IX_HomeworkAssignments_AcademicYearId");
	}
}
