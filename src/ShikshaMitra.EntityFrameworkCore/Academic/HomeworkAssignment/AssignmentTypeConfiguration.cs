using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.HomeworkAssignment;

/// <summary>
/// EF Core entity configuration for AssignmentType
/// </summary>
public class AssignmentTypeConfiguration : IEntityTypeConfiguration<AssignmentType>
{
	public void Configure(EntityTypeBuilder<AssignmentType> builder)
	{
		// Table configuration
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "AssignmentTypes",
			ShikshaMitraConsts.DbSchema
		);

		// ABP conventions (CreationTime, CreatorId, etc.)
		builder.ConfigureByConvention();

		// Properties
		builder
			.Property(x => x.Name)
			.IsRequired()
			.HasMaxLength(AssignmentTypeConsts.MaxNameLength);

		builder.Property(x => x.AcademicMediumId).IsRequired();

		// Indexes
		builder
			.HasIndex(x => new { x.AcademicMediumId, x.Name })
			.IsUnique()
			.HasDatabaseName("IX_AssignmentTypes_AcademicMediumId_Name")
			.HasFilter("\"IsDeleted\" = false");

		// Foreign Keys
		builder
			.HasOne(x => x.AcademicMedium)
			.WithMany()
			.HasForeignKey(x => x.AcademicMediumId)
			.OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete
	}
}
