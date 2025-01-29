
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class EntranceExamSlotConfiguration : IEntityTypeConfiguration<EntranceExamSlot>
{
	public void Configure(EntityTypeBuilder<EntranceExamSlot> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EntranceExamSlots", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}