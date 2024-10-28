using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AutomatedNotificationsConfiguration : IEntityTypeConfiguration<AutomatedNotificationsSettings>
{
	public void Configure(EntityTypeBuilder<AutomatedNotificationsSettings> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AutomatedNotificationsSettings",
			ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}