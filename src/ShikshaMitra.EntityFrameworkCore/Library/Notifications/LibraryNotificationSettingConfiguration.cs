using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Notifications;

public class LibraryNotificationSettingConfiguration : IEntityTypeConfiguration<LibraryNotificationSetting>
{
	public void Configure(EntityTypeBuilder<LibraryNotificationSetting> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "LibraryNotificationSettings", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.EventType).IsRequired().HasMaxLength(80);
		builder.Property(x => x.EventGroup).IsRequired().HasMaxLength(60);
		builder.Property(x => x.EventName).IsRequired().HasMaxLength(120);
		builder.Property(x => x.RecipientRole).IsRequired().HasMaxLength(60);
		builder.Property(x => x.Channel).IsRequired().HasMaxLength(40);
		builder.Property(x => x.SubjectTemplate).IsRequired().HasMaxLength(300);
		builder.Property(x => x.MessageTemplate).IsRequired().HasMaxLength(2000);

		builder.HasIndex(x => new { x.EventType, x.RecipientRole, x.Channel }).IsUnique();
		builder.HasIndex(x => x.EventGroup);
	}
}
