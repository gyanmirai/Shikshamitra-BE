using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Communication.Notifications;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<NotificationRule> NotificationRules { get; set; }
	public DbSet<CommunicationTemplateRevision> CommunicationTemplateRevisions { get; set; }
	public DbSet<WhatsAppTemplateBinding> WhatsAppTemplateBindings { get; set; }
	public DbSet<NotificationIntent> NotificationIntents { get; set; }
	public DbSet<NotificationDispatch> NotificationDispatches { get; set; }
	public DbSet<NotificationPreference> NotificationPreferences { get; set; }
	public DbSet<NotificationInboxItem> NotificationInboxItems { get; set; }
	public DbSet<WebPushSubscription> WebPushSubscriptions { get; set; }
	public DbSet<NotificationScheduleCursor> NotificationScheduleCursors { get; set; }
	public DbSet<NotificationProviderEvent> NotificationProviderEvents { get; set; }
	public DbSet<NotificationSendReview> NotificationSendReviews { get; set; }
}
