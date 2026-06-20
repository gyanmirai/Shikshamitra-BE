using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Communication;

namespace ShikshaMitra.EntityFrameworkCore.Communication;

public class CommunicationCampaignConfiguration : IEntityTypeConfiguration<CommunicationCampaign>
{
	public void Configure(EntityTypeBuilder<CommunicationCampaign> builder)
	{
		builder.ToTable("CommunicationCampaigns");

		builder.Property(x => x.CampaignType).IsRequired().HasMaxLength(64);
		builder.Property(x => x.SourceEntityType).HasMaxLength(80);
		builder.Property(x => x.SourceReference).HasMaxLength(256);
		builder.Property(x => x.Title).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Subject).HasMaxLength(256);
		builder.Property(x => x.Body).IsRequired();
		builder.Property(x => x.AudienceDefinitionJson).IsRequired();
		builder.Property(x => x.ChannelsJson).IsRequired();
		builder.Property(x => x.FallbackOrderJson).IsRequired();
		builder.Property(x => x.AttachmentsJson).IsRequired();
		builder.Property(x => x.LinkUrl).HasMaxLength(1000);
		builder.Property(x => x.LanguageCode).IsRequired().HasMaxLength(16);
		builder.Property(x => x.RejectionReason).HasMaxLength(1000);

		builder.HasIndex(x => new { x.TenantId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.SourceModule, x.CampaignType });
		builder.HasIndex(x => new { x.TenantId, x.SourceModule, x.SourceEntityType, x.SourceEntityId })
			.HasDatabaseName("IX_CommCampaigns_Tenant_SourceEntity");
		builder.HasIndex(x => new { x.TenantId, x.ScheduledAt });

		builder.HasMany<CommunicationRecipientSnapshot>()
			.WithOne()
			.HasForeignKey(x => x.CampaignId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasMany<DeliveryAttempt>()
			.WithOne()
			.HasForeignKey(x => x.CampaignId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasMany<CommunicationReply>()
			.WithOne()
			.HasForeignKey(x => x.CampaignId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasMany<UsageLedgerEntry>()
			.WithOne()
			.HasForeignKey(x => x.CampaignId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}

public class CommunicationTemplateConfiguration : IEntityTypeConfiguration<CommunicationTemplate>
{
	public void Configure(EntityTypeBuilder<CommunicationTemplate> builder)
	{
		builder.ToTable("CommunicationTemplates");

		builder.Property(x => x.TemplateName).IsRequired().HasMaxLength(160);
		builder.Property(x => x.TargetRole).HasMaxLength(80);
		builder.Property(x => x.Category).IsRequired().HasMaxLength(80);
		builder.Property(x => x.Subject).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Body).IsRequired();
		builder.Property(x => x.LanguageCode).IsRequired().HasMaxLength(16);
		builder.Property(x => x.ChannelsJson).IsRequired();
		builder.Property(x => x.VariablesJson).IsRequired();
		builder.Property(x => x.HashtagsJson).IsRequired();

		builder.HasIndex(x => new { x.TenantId, x.AcademicMediumId, x.TemplateName, x.LanguageCode })
			.HasDatabaseName("IX_CommTemplates_Tenant_Medium_Name_Language");
	}
}

public class ChannelProviderSettingConfiguration : IEntityTypeConfiguration<ChannelProviderSetting>
{
	public void Configure(EntityTypeBuilder<ChannelProviderSetting> builder)
	{
		builder.ToTable("CommunicationChannelProviderSettings");

		builder.Property(x => x.ProviderName).IsRequired().HasMaxLength(120);
		builder.Property(x => x.SenderId).HasMaxLength(120);
		builder.Property(x => x.SecretReference).HasMaxLength(256);
		builder.Property(x => x.FallbackOrderJson).IsRequired();

		builder.HasIndex(x => new { x.TenantId, x.Channel, x.ProviderName })
			.HasDatabaseName("IX_CommProvider_Tenant_Channel_Provider");
	}
}

public class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
	public void Configure(EntityTypeBuilder<ConsentRecord> builder)
	{
		builder.ToTable("CommunicationConsentRecords");

		builder.Property(x => x.RecipientType).IsRequired().HasMaxLength(80);
		builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Destination).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Source).IsRequired().HasMaxLength(120);
		builder.Property(x => x.Notes).HasMaxLength(1000);

		builder.HasIndex(x => new { x.TenantId, x.Channel, x.Destination })
			.HasDatabaseName("IX_CommConsent_Tenant_Channel_Destination");
	}
}

public class CommunicationRecipientSnapshotConfiguration : IEntityTypeConfiguration<CommunicationRecipientSnapshot>
{
	public void Configure(EntityTypeBuilder<CommunicationRecipientSnapshot> builder)
	{
		builder.ToTable("CommunicationRecipientSnapshots");

		builder.Property(x => x.RecipientType).IsRequired().HasMaxLength(80);
		builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Email).HasMaxLength(256);
		builder.Property(x => x.MobileNumber).HasMaxLength(32);
		builder.Property(x => x.PreferredLanguageCode).IsRequired().HasMaxLength(16);
		builder.Property(x => x.ChannelEligibilityJson).IsRequired();
		builder.Property(x => x.BlockedReason).HasMaxLength(1000);

		builder.HasIndex(x => new { x.TenantId, x.CampaignId });
		builder.HasIndex(x => new { x.TenantId, x.RecipientType, x.RecipientId });
	}
}

public class DeliveryAttemptConfiguration : IEntityTypeConfiguration<DeliveryAttempt>
{
	public void Configure(EntityTypeBuilder<DeliveryAttempt> builder)
	{
		builder.ToTable("CommunicationDeliveryAttempts");

		builder.Property(x => x.Destination).IsRequired().HasMaxLength(256);
		builder.Property(x => x.ProviderName).HasMaxLength(120);
		builder.Property(x => x.ProviderMessageId).HasMaxLength(256);
		builder.Property(x => x.ErrorCode).HasMaxLength(120);
		builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
		builder.Property(x => x.EstimatedCost).HasColumnType("numeric(18,2)");
		builder.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(8);

		builder.HasIndex(x => new { x.TenantId, x.CampaignId, x.Channel });
		builder.HasIndex(x => new { x.TenantId, x.Status });

		builder.HasOne<CommunicationRecipientSnapshot>()
			.WithMany()
			.HasForeignKey(x => x.RecipientSnapshotId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}

public class CommunicationReplyConfiguration : IEntityTypeConfiguration<CommunicationReply>
{
	public void Configure(EntityTypeBuilder<CommunicationReply> builder)
	{
		builder.ToTable("CommunicationReplies");

		builder.Property(x => x.FromAddress).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Body).IsRequired();
		builder.Property(x => x.AssignedDepartment).HasMaxLength(120);
		builder.Property(x => x.InternalNotes).HasMaxLength(1000);

		builder.HasIndex(x => new { x.TenantId, x.Status, x.ReceivedAt });
		builder.HasIndex(x => new { x.TenantId, x.CampaignId });

		builder.HasOne<DeliveryAttempt>()
			.WithMany()
			.HasForeignKey(x => x.DeliveryAttemptId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne<CommunicationRecipientSnapshot>()
			.WithMany()
			.HasForeignKey(x => x.RecipientSnapshotId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}

public class UsageLedgerEntryConfiguration : IEntityTypeConfiguration<UsageLedgerEntry>
{
	public void Configure(EntityTypeBuilder<UsageLedgerEntry> builder)
	{
		builder.ToTable("CommunicationUsageLedgerEntries");

		builder.Property(x => x.ProviderName).IsRequired().HasMaxLength(120);
		builder.Property(x => x.EstimatedCost).HasColumnType("numeric(18,2)");
		builder.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(8);

		builder.HasIndex(x => new { x.TenantId, x.UsageDate, x.Channel });
		builder.HasIndex(x => new { x.TenantId, x.CampaignId });
	}
}

public class CommunicationApprovalRuleConfiguration : IEntityTypeConfiguration<CommunicationApprovalRule>
{
	public void Configure(EntityTypeBuilder<CommunicationApprovalRule> builder)
	{
		builder.ToTable("CommunicationApprovalRules");

		builder.Property(x => x.RuleName).IsRequired().HasMaxLength(160);
		builder.Property(x => x.CampaignType).HasMaxLength(64);

		builder.HasIndex(x => new { x.TenantId, x.IsEnabled });
		builder.HasIndex(x => new { x.TenantId, x.CampaignType, x.SourceModule, x.Channel })
			.HasDatabaseName("IX_CommApprovalRules_Tenant_Scope");
	}
}
