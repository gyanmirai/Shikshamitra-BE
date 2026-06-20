using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication;

public class CommunicationRecipientSnapshot : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid CampaignId { get; set; }
	public string RecipientType { get; set; } = string.Empty;
	public Guid? RecipientId { get; set; }
	public Guid? UserId { get; set; }
	public string DisplayName { get; set; } = string.Empty;
	public string? Email { get; set; }
	public string? MobileNumber { get; set; }
	public string PreferredLanguageCode { get; set; } = "en";
	public string ChannelEligibilityJson { get; set; } = "{}";
	public bool IsParentDefault { get; set; }
	public bool IsEligible { get; set; } = true;
	public string? BlockedReason { get; set; }

	protected CommunicationRecipientSnapshot()
	{
	}

	public CommunicationRecipientSnapshot(Guid id, Guid? tenantId) : base(id)
	{
		TenantId = tenantId;
	}
}
