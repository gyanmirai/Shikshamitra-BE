using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;

public class NotificationInfoConfiguration : IEntityTypeConfiguration<NotificationInfo>
{
	public void Configure(EntityTypeBuilder<NotificationInfo> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "NotificationInfos", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}