using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Communication.Notifications;

namespace ShikshaMitra.EntityFrameworkCore.Communication.Notifications;

public sealed class NotificationRuleConfiguration : IEntityTypeConfiguration<NotificationRule>
{
	public void Configure(EntityTypeBuilder<NotificationRule> builder)
	{
		NotificationMapping.Configure(builder, "NotificationRules");
		builder.Property(x => x.EventKey).IsRequired().HasMaxLength(160);
		builder.Property(x => x.ScopeKey).IsRequired().HasMaxLength(256);
		builder.Property(x => x.ConfigurationJson).IsRequired().HasMaxLength(65536);
		builder.HasIndex(x => new { x.TenantId, x.EventKey, x.ScopeKey }).IsUnique()
			.HasFilter("\"IsDeleted\" = false").HasDatabaseName("UX_NotificationRules_Tenant_Event_Scope");
	}
}

public sealed class NotificationSendReviewConfiguration : IEntityTypeConfiguration<NotificationSendReview>
{
	public void Configure(EntityTypeBuilder<NotificationSendReview> builder)
	{
		NotificationMapping.Configure(builder, "NotificationSendReviews");
		builder.Property(x => x.Digest).IsRequired().HasMaxLength(128);
		builder.Property(x => x.IdempotencyKey).HasMaxLength(192);
		builder.Property(x => x.ProtectedRequest).HasMaxLength(NotificationSendReview.MaximumProtectedRequestLength);
		builder.HasIndex(x => new { x.TenantId, x.ActorUserId, x.IdempotencyKey }).IsUnique()
			.HasFilter("\"IdempotencyKey\" IS NOT NULL").HasDatabaseName("UX_NotificationReviews_Tenant_Actor_Request");
		builder.HasIndex(x => x.ExpiresAt);
		builder.HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique()
			.HasFilter("\"OperationId\" IS NOT NULL").HasDatabaseName("UX_NotificationReviews_Tenant_Operation");
	}
}

public sealed class NotificationScheduleCursorConfiguration : IEntityTypeConfiguration<NotificationScheduleCursor>
{
	public void Configure(EntityTypeBuilder<NotificationScheduleCursor> builder)
	{
		NotificationMapping.Configure(builder, "NotificationScheduleCursors");
		builder.Property(x => x.SchedulerKey).IsRequired().HasMaxLength(192);
		builder.Property(x => x.CursorJson).IsRequired().HasMaxLength(65536);
		builder.HasIndex(x => new { x.TenantId, x.SchedulerKey }).IsUnique();
		builder.HasIndex(x => new { x.NextScanAt, x.TenantId });
	}
}
