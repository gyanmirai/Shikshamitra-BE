using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;

namespace ShikshaMitra.EntityFrameworkCore.Communication.Notifications;

public sealed class NotificationIntentConfiguration : IEntityTypeConfiguration<NotificationIntent>
{
	public void Configure(EntityTypeBuilder<NotificationIntent> builder)
	{
		NotificationMapping.Configure(builder, "NotificationIntents");
		builder.ToTable("NotificationIntents", table => table.HasCheckConstraint("CK_NotificationIntent_Origin",
			"((\"RuleId\" IS NOT NULL AND \"RuleId\" <> '00000000-0000-0000-0000-000000000000' AND \"RuleRevision\" > 0) " +
			"OR (\"Origin\" = 1 AND \"RuleId\" IS NULL AND \"RuleRevision\" = 0)) AND " +
			"\"Origin\" IN (0, 1) AND (\"Origin\" = 0 OR (\"CampaignId\" IS NOT NULL AND \"ActorUserId\" IS NOT NULL AND " +
			"\"CampaignId\" <> '00000000-0000-0000-0000-000000000000' AND \"ActorUserId\" <> '00000000-0000-0000-0000-000000000000'))"));
		builder.Property(x => x.EventKey).IsRequired().HasMaxLength(160);
		builder.Property(x => x.SourceType).IsRequired().HasMaxLength(80);
		builder.Property(x => x.SourceRevision).IsRequired().HasMaxLength(128);
		builder.Property(x => x.DeduplicationKey).IsRequired().HasMaxLength(128);
		builder.Property(x => x.AffectedRecipientsJson).IsRequired().HasMaxLength(65536);
		builder.Property(x => x.TemplateRevisionIdsJson).IsRequired().HasMaxLength(65536);
		builder.Property(x => x.RuleConfigurationJson).IsRequired().HasMaxLength(65536).HasDefaultValue("{}");
		builder.Property(x => x.Origin).HasDefaultValue(NotificationIntentOrigin.Automatic);
		builder.Property(x => x.ErrorCode).HasMaxLength(160);
		builder.HasIndex(x => new { x.TenantId, x.DeduplicationKey }).IsUnique();
		builder.HasIndex(x => new { x.Status, x.DueAt, x.TenantId });
		builder.HasIndex(x => new { x.TenantId, x.SourceType, x.SourceId });
		builder.HasIndex(x => new { x.TenantId, x.Status, x.LeaseExpiresAt });
		builder.HasOne<NotificationRule>().WithMany().HasForeignKey(x => new { x.TenantId, x.RuleId })
			.HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
	}
}

public sealed class NotificationDispatchConfiguration : IEntityTypeConfiguration<NotificationDispatch>
{
	public void Configure(EntityTypeBuilder<NotificationDispatch> builder)
	{
		NotificationMapping.Configure(builder, "NotificationDispatches");
		builder.ToTable("NotificationDispatches", table => table.HasCheckConstraint("CK_NotificationDispatch_IoMarkers",
			"((\"IoStartedAt\" IS NULL AND \"IoLeaseId\" IS NULL) OR (\"IoStartedAt\" IS NOT NULL AND \"IoLeaseId\" IS NOT NULL)) " +
			"AND (\"AcceptedAt\" IS NULL OR \"IoStartedAt\" IS NOT NULL)"));
		builder.Property(x => x.RecipientReferenceJson).IsRequired().HasMaxLength(65536);
		builder.Property(x => x.RecipientContextKey).IsRequired().HasMaxLength(192);
		builder.Property(x => x.DestinationFingerprint).IsRequired().HasMaxLength(128);
		builder.Property(x => x.ProtectedMessageJson).IsRequired().HasMaxLength(131072);
		builder.Property(x => x.ContentHash).IsRequired().HasMaxLength(128);
		builder.Property(x => x.DeduplicationKey).IsRequired().HasMaxLength(128);
		builder.Property(x => x.ProviderMessageId).HasMaxLength(256);
		builder.Property(x => x.ErrorCode).HasMaxLength(160);
		builder.HasIndex(x => new { x.TenantId, x.DeduplicationKey }).IsUnique();
		builder.HasIndex(x => new { x.Status, x.NextAttemptAt, x.TenantId });
		builder.HasIndex(x => new { x.TenantId, x.Status, x.LeaseExpiresAt });
		builder.HasOne<NotificationIntent>().WithMany().HasForeignKey(x => new { x.TenantId, x.IntentId })
			.HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<CommunicationTemplateRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.TemplateRevisionId })
			.HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
	}
}

public sealed class NotificationProviderEventConfiguration : IEntityTypeConfiguration<NotificationProviderEvent>
{
	public void Configure(EntityTypeBuilder<NotificationProviderEvent> builder)
	{
		NotificationMapping.Configure(builder, "NotificationProviderEvents");
		builder.Property(x => x.ProviderEventKey).IsRequired().HasMaxLength(192);
		builder.Property(x => x.ProviderMessageId).HasMaxLength(256);
		builder.Property(x => x.ProtectedPayload).IsRequired().HasMaxLength(131072);
		builder.HasIndex(x => new { x.TenantId, x.ProviderSettingId, x.ProviderEventKey }).IsUnique()
			.HasDatabaseName("UX_NotificationProviderEvent_Dedup");
		builder.HasIndex(x => new { x.ProcessedAt, x.ReceivedAt });
		builder.HasOne<ChannelProviderSetting>().WithMany().HasForeignKey(x => x.ProviderSettingId).OnDelete(DeleteBehavior.Restrict);
	}
}
