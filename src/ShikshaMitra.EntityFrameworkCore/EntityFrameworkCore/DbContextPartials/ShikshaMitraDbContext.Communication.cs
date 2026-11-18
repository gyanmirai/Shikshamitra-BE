using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<CommunicationCampaign> CommunicationCampaigns { get; set; }
	public DbSet<CommunicationTemplate> CommunicationTemplates { get; set; }
	public DbSet<ChannelProviderSetting> CommunicationChannelProviderSettings { get; set; }
	public DbSet<ConsentRecord> CommunicationConsentRecords { get; set; }
	public DbSet<CommunicationRecipientSnapshot> CommunicationRecipientSnapshots { get; set; }
	public DbSet<DeliveryAttempt> CommunicationDeliveryAttempts { get; set; }
	public DbSet<CommunicationReply> CommunicationReplies { get; set; }
	public DbSet<UsageLedgerEntry> CommunicationUsageLedgerEntries { get; set; }
	public DbSet<CommunicationApprovalRule> CommunicationApprovalRules { get; set; }

	protected override void UpdateConcurrencyStamp(EntityEntry entry)
	{
		if (entry.State == EntityState.Modified && entry.Entity is (NotificationDispatch or NotificationIntent or NotificationRule))
		{
			var stamp = entry.Property(nameof(IHasConcurrencyStamp.ConcurrencyStamp));
			// The aggregate can rotate the stamp before saving. EF must compare the value loaded with the row.
			if (!Equals(stamp.OriginalValue, stamp.CurrentValue)) return;
		}
		base.UpdateConcurrencyStamp(entry);
	}
}
