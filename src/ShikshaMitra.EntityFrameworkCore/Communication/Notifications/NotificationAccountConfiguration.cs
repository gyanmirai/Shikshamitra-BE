using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Communication.Notifications;

namespace ShikshaMitra.EntityFrameworkCore.Communication.Notifications;

public sealed class NotificationInboxItemConfiguration : IEntityTypeConfiguration<NotificationInboxItem>
{
	public void Configure(EntityTypeBuilder<NotificationInboxItem> builder)
	{
		NotificationMapping.Configure(builder, "NotificationInboxItems");
		builder.Property(x => x.SourceType).IsRequired().HasMaxLength(80);
		builder.Property(x => x.ContextKey).IsRequired().HasMaxLength(192);
		builder.Property(x => x.LogicalKey).IsRequired().HasMaxLength(128);
		builder.Property(x => x.Title).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Body).IsRequired().HasMaxLength(NotificationInboxItem.MaximumBodyLength);
		builder.Property(x => x.RelativeLink).IsRequired().HasMaxLength(NotificationInboxItem.MaximumLinkLength);
		builder.HasIndex(x => new { x.TenantId, x.UserId, x.LogicalKey, x.ContextKey }).IsUnique()
			.HasDatabaseName("UX_NotificationInbox_User_Context_Logical");
		builder.HasIndex(x => new { x.TenantId, x.UserId, x.ReadAt, x.CreationTime });
		builder.HasOne<NotificationDispatch>().WithMany().HasForeignKey(x => new { x.TenantId, x.DispatchId })
			.HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
	}
}

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
	public void Configure(EntityTypeBuilder<NotificationPreference> builder)
	{
		NotificationMapping.Configure(builder, "NotificationPreferences");
		builder.Property(x => x.ContextKey).IsRequired().HasMaxLength(192);
		builder.Property(x => x.Category).IsRequired().HasMaxLength(80);
		builder.Property(x => x.ConfigurationJson).IsRequired().HasMaxLength(65536);
		builder.Property(x => x.LanguageCode).IsRequired().HasMaxLength(16);
		builder.HasIndex(x => new { x.TenantId, x.UserId, x.ContextKey, x.Category }).IsUnique()
			.HasDatabaseName("UX_NotificationPreference_User_Context_Category");
	}
}

public sealed class WebPushSubscriptionConfiguration : IEntityTypeConfiguration<WebPushSubscription>
{
	public void Configure(EntityTypeBuilder<WebPushSubscription> builder)
	{
		NotificationMapping.Configure(builder, "WebPushSubscriptions");
		builder.Property(x => x.EndpointHash).IsRequired().HasMaxLength(128);
		builder.Property(x => x.ProtectedSubscription).IsRequired().HasMaxLength(16384);
		builder.Property(x => x.VapidKeyVersion).IsRequired().HasMaxLength(64);
		builder.HasIndex(x => new { x.TenantId, x.EndpointHash }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.UserId, x.IsEnabled });
		builder.HasIndex(x => new { x.TenantId, x.BrowserInstallationId }).IsUnique()
			.HasFilter("\"IsEnabled\" = true AND \"IsDeleted\" = false").HasDatabaseName("UX_WebPush_Active_Installation");
	}
}
