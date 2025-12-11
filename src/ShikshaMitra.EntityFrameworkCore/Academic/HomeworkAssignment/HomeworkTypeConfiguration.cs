using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.HomeworkAssignment;

/// <summary>
/// EF Core entity configuration for HomeworkType
/// </summary>
public class HomeworkTypeConfiguration : IEntityTypeConfiguration<HomeworkType>
{
	public void Configure(EntityTypeBuilder<HomeworkType> builder)
	{
		// Table configuration
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "HomeworkTypes",
			ShikshaMitraConsts.DbSchema
		);

		// ABP conventions (CreationTime, CreatorId, etc.)
		builder.ConfigureByConvention();

		// Properties
		builder
			.Property(x => x.Name)
			.IsRequired()
			.HasMaxLength(HomeworkTypeConsts.MaxNameLength);

		builder.Property(x => x.AcademicMediumId).IsRequired();

		// Indexes
		builder
			.HasIndex(x => new { x.AcademicMediumId, x.Name })
			.IsUnique()
			.HasDatabaseName("IX_HomeworkTypes_AcademicMediumId_Name");

		// Foreign Keys
		builder
			.HasOne(x => x.AcademicMedium)
			.WithMany()
			.HasForeignKey(x => x.AcademicMediumId)
			.OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete
	}
}
