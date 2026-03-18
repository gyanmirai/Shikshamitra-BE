using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.EventAndHolidays;

public class EventAndHolidayConfiguration : IEntityTypeConfiguration<EventAndHoliday>
{
	public void Configure(EntityTypeBuilder<EventAndHoliday> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EventAndHolidays", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

	}
}