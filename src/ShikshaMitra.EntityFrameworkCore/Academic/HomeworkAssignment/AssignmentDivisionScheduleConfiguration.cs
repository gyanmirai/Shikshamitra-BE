using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.HomeworkAssignment;

/// <summary>
/// EF Core entity configuration for AssignmentDivisionSchedule
/// </summary>
public class AssignmentDivisionScheduleConfiguration
	: IEntityTypeConfiguration<AssignmentDivisionSchedule>
{
	public void Configure(EntityTypeBuilder<AssignmentDivisionSchedule> builder)
	{
		// Table configuration
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "AssignmentDivisionSchedules",
			ShikshaMitraConsts.DbSchema
		);

		// ABP conventions
		builder.ConfigureByConvention();

		// Properties
		builder.Property(x => x.AssignmentId).IsRequired();
		builder.Property(x => x.DivisionId).IsRequired();
		builder.Property(x => x.PublishDate).IsRequired(false);
		builder.Property(x => x.DueDate).IsRequired();
		builder.Property(x => x.PublishedAt).IsRequired(false);
		builder.Property(x => x.IsPublished).IsRequired().HasDefaultValue(false);

		// Indexes for performance
		builder
			.HasIndex(x => x.AssignmentId)
			.HasDatabaseName("IX_AssignmentDivisionSchedules_AssignmentId");

		builder
			.HasIndex(x => x.DivisionId)
			.HasDatabaseName("IX_AssignmentDivisionSchedules_DivisionId");

		// Index for scheduled publish job queries (finds unpublished schedules with past publish dates)
		builder
			.HasIndex(x => x.PublishDate)
			.HasDatabaseName("IX_AssignmentDivisionSchedules_PublishDate")
			.HasFilter("[PublishDate] IS NOT NULL AND [IsPublished] = 0");

		// Unique constraint: one assignment can't have duplicate divisions
		builder
			.HasIndex(x => new { x.AssignmentId, x.DivisionId })
			.IsUnique()
			.HasDatabaseName("IX_AssignmentDivisionSchedules_AssignmentId_DivisionId");

		// Foreign Keys
		builder
			.HasOne(x => x.Assignment)
			.WithMany(a => a.DivisionSchedules)
			.HasForeignKey(x => x.AssignmentId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
