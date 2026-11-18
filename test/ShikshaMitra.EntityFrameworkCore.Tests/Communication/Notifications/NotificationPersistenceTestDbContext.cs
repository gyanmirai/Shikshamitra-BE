using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Communication;
using ShikshaMitra.EntityFrameworkCore.Communication;
using ShikshaMitra.EntityFrameworkCore.Communication.Notifications;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.EntityFrameworkCore.Modeling;
using NotificationPreferenceMapping = ShikshaMitra.EntityFrameworkCore.Communication.Notifications.NotificationPreferenceConfiguration;

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
		builder.Entity<CommunicationCampaign>().ConfigureByConvention();
		builder.Entity<CommunicationCampaign>().ToTable("CommunicationCampaigns");
		builder.Entity<CommunicationCampaign>().Ignore(x => x.AnnouncementType);
		builder.Entity<CommunicationRecipientSnapshot>().ConfigureByConvention();
		builder.ApplyConfiguration(new CommunicationRecipientSnapshotConfiguration());
		builder.Entity<DeliveryAttempt>().ConfigureByConvention();
		builder.ApplyConfiguration(new DeliveryAttemptConfiguration());
		builder.ApplyConfiguration(new ChannelProviderSettingConfiguration());
		builder.Entity<ConsentRecord>().ConfigureByConvention();
		builder.ApplyConfiguration(new ConsentRecordConfiguration());
		builder.ApplyConfiguration(new NotificationRuleConfiguration());
		builder.ApplyConfiguration(new NotificationTemplateRevisionConfiguration());
		builder.ApplyConfiguration(new WhatsAppTemplateBindingConfiguration());
		builder.ApplyConfiguration(new NotificationIntentConfiguration());
		builder.ApplyConfiguration(new NotificationDispatchConfiguration());
		builder.ApplyConfiguration(new NotificationInboxItemConfiguration());
		builder.ApplyConfiguration(new NotificationPreferenceMapping());
		builder.ApplyConfiguration(new WebPushSubscriptionConfiguration());
		builder.ApplyConfiguration(new NotificationScheduleCursorConfiguration());
		builder.ApplyConfiguration(new NotificationProviderEventConfiguration());
		builder.ApplyConfiguration(new NotificationSendReviewConfiguration());
		builder.Entity<AcademicYear>().ConfigureByConvention();
		builder.ApplyConfiguration(new AcademicCalendarConfiguration());
		builder.ApplyConfiguration(new AcademicCalendarDayConfiguration());
	}
}
