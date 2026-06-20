using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Communication;

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
}
