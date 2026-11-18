using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;

namespace ShikshaMitra.EntityFrameworkCore.Communication.Notifications;

public sealed class NotificationTemplateRevisionConfiguration : IEntityTypeConfiguration<CommunicationTemplateRevision>
{
	public void Configure(EntityTypeBuilder<CommunicationTemplateRevision> builder)
	{
		NotificationMapping.Configure(builder, "CommunicationTemplateRevisions");
		builder.Property(x => x.EventKey).IsRequired().HasMaxLength(160);
		builder.Property(x => x.RecipientKind).IsRequired().HasMaxLength(64);
		builder.Property(x => x.LanguageCode).IsRequired().HasMaxLength(16);
		builder.Property(x => x.Subject).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Body).IsRequired().HasMaxLength(65536);
		builder.Property(x => x.ContentHash).IsRequired().HasMaxLength(128);
		builder.HasIndex(x => new { x.TenantId, x.TemplateId, x.Revision }).IsUnique()
			.HasDatabaseName("UX_NotificationRevision_Tenant_Template_Version");
		builder.HasOne<CommunicationTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
	}
}

public sealed class WhatsAppTemplateBindingConfiguration : IEntityTypeConfiguration<WhatsAppTemplateBinding>
{
	public void Configure(EntityTypeBuilder<WhatsAppTemplateBinding> builder)
	{
		NotificationMapping.Configure(builder, "WhatsAppTemplateBindings");
		builder.Property(x => x.ProviderTemplateName).IsRequired().HasMaxLength(160);
		builder.Property(x => x.ProviderTemplateId).HasMaxLength(256);
		builder.Property(x => x.LanguageCode).IsRequired().HasMaxLength(16);
		builder.Property(x => x.ParameterMappingsJson).IsRequired().HasMaxLength(65536);
		builder.HasIndex(x => new { x.TenantId, x.TemplateRevisionId, x.ProviderSettingId }).IsUnique()
			.HasDatabaseName("UX_WhatsAppBinding_Tenant_Revision_Provider");
		builder.HasOne<CommunicationTemplateRevision>().WithMany()
			.HasForeignKey(x => new { x.TenantId, x.TemplateRevisionId })
			.HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<ChannelProviderSetting>().WithMany().HasForeignKey(x => x.ProviderSettingId).OnDelete(DeleteBehavior.Restrict);
	}
}
