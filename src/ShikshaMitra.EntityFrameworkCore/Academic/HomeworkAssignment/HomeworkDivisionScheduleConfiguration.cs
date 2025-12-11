using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.HomeworkAssignment;

/// <summary>
/// EF Core entity configuration for HomeworkDivisionSchedule
/// </summary>
public class HomeworkDivisionScheduleConfiguration
	: IEntityTypeConfiguration<HomeworkDivisionSchedule>
{
	public void Configure(EntityTypeBuilder<HomeworkDivisionSchedule> builder)
	{
		// Table configuration
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "HomeworkDivisionSchedules",
			ShikshaMitraConsts.DbSchema
		);

		// ABP conventions
		builder.ConfigureByConvention();

		// Properties
		builder.Property(x => x.HomeworkId).IsRequired();
		builder.Property(x => x.DivisionId).IsRequired();
		builder.Property(x => x.PublishDate).IsRequired(false);
		builder.Property(x => x.DueDate).IsRequired();
		builder.Property(x => x.PublishedAt).IsRequired(false);
		builder.Property(x => x.IsPublished).IsRequired().HasDefaultValue(false);

		// Indexes
		builder
			.HasIndex(x => x.HomeworkId)
			.HasDatabaseName("IX_HomeworkDivisionSchedules_HomeworkId");

		builder
			.HasIndex(x => x.DivisionId)
			.HasDatabaseName("IX_HomeworkDivisionSchedules_DivisionId");

		builder
			.HasIndex(x => x.PublishDate)
			.HasDatabaseName("IX_HomeworkDivisionSchedules_PublishDate")
			.HasFilter("\"PublishDate\" IS NOT NULL AND \"IsPublished\" = false");

		builder
			.HasIndex(x => new { x.HomeworkId, x.DivisionId })
			.IsUnique()
			.HasDatabaseName("IX_HomeworkDivisionSchedules_HomeworkId_DivisionId");

		// Foreign Keys
		builder
			.HasOne(x => x.Homework)
			.WithMany(h => h.DivisionSchedules)
			.HasForeignKey(x => x.HomeworkId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
