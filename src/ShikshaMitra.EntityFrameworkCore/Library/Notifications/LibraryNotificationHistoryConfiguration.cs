using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Notifications;

public class LibraryNotificationHistoryConfiguration : IEntityTypeConfiguration<LibraryNotificationHistory>
{
	public void Configure(EntityTypeBuilder<LibraryNotificationHistory> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "LibraryNotificationHistories", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.EventType).IsRequired().HasMaxLength(80);
		builder.Property(x => x.EventGroup).IsRequired().HasMaxLength(60);
		builder.Property(x => x.EventName).IsRequired().HasMaxLength(120);
		builder.Property(x => x.Channel).IsRequired().HasMaxLength(40);
		builder.Property(x => x.RecipientRole).IsRequired().HasMaxLength(60);
		builder.Property(x => x.RecipientName).HasMaxLength(200);
		builder.Property(x => x.RecipientEmail).HasMaxLength(256);
		builder.Property(x => x.Subject).IsRequired().HasMaxLength(300);
		builder.Property(x => x.Message).IsRequired().HasMaxLength(2000);
		builder.Property(x => x.Status).IsRequired().HasMaxLength(40);
		builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
		builder.Property(x => x.RelatedEntityType).HasMaxLength(80);
		builder.Property(x => x.DeduplicationKey).HasMaxLength(300);

		builder.HasIndex(x => x.EventType);
		builder.HasIndex(x => x.Channel);
		builder.HasIndex(x => x.Status);
		builder.HasIndex(x => x.RecipientMemberId);
		builder.HasIndex(x => x.RelatedEntityId);
		builder.HasIndex(x => x.DeduplicationKey);
	}
}
