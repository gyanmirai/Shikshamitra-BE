using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Communication;
using ShikshaMitra.EntityFrameworkCore.Communication;
using ShikshaMitra.EntityFrameworkCore.Communication.Notifications;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Communication.Notifications;

public sealed class NotificationPersistenceTestDbContext(DbContextOptions<NotificationPersistenceTestDbContext> options) : DbContext(options)
{
	protected override void OnModelCreating(ModelBuilder builder)
	{
		builder.Entity<CommunicationTemplate>().ConfigureByConvention();
		builder.ApplyConfiguration(new CommunicationTemplateConfiguration());
		builder.Entity<ChannelProviderSetting>().ConfigureByConvention();
		builder.Entity<CommunicationReply>().ConfigureByConvention();
		builder.Entity<CommunicationReply>().ToTable("CommunicationReplies");
		builder.ApplyConfiguration(new ChannelProviderSettingConfiguration());
		builder.ApplyConfiguration(new NotificationRuleConfiguration());
		builder.ApplyConfiguration(new NotificationTemplateRevisionConfiguration());
		builder.ApplyConfiguration(new WhatsAppTemplateBindingConfiguration());
		builder.ApplyConfiguration(new NotificationIntentConfiguration());
		builder.ApplyConfiguration(new NotificationDispatchConfiguration());
		builder.ApplyConfiguration(new NotificationInboxItemConfiguration());
		builder.ApplyConfiguration(new NotificationPreferenceConfiguration());
		builder.ApplyConfiguration(new WebPushSubscriptionConfiguration());
		builder.ApplyConfiguration(new NotificationScheduleCursorConfiguration());
		builder.ApplyConfiguration(new NotificationProviderEventConfiguration());
		builder.ApplyConfiguration(new NotificationSendReviewConfiguration());
	}
}
