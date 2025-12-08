using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Settings;

public class EntranceExamSlotConfiguration : IEntityTypeConfiguration<EntranceExamSlot>
{
	public void Configure(EntityTypeBuilder<EntranceExamSlot> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "EntranceExamSlots",
			ShikshaMitraConsts.DbSchema
		);

		builder.ConfigureByConvention();

		// Configure SlotName property
		builder.Property(x => x.SlotName)
			.HasMaxLength(100)
			.IsRequired(false);

		// Configure AcademicYearId as required
		builder.Property(x => x.AcademicYearId)
			.IsRequired();

		// Create index for better query performance
		builder.HasIndex(x => new { x.AcademicYearId, x.StartDate, x.TenantId });
	}
}
