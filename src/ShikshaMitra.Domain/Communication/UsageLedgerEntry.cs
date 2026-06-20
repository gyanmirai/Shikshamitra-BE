using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication;

public class UsageLedgerEntry : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid? CampaignId { get; set; }
	public CommunicationChannelType Channel { get; set; }
	public string ProviderName { get; set; } = string.Empty;
	public DateOnly UsageDate { get; set; }
	public int AttemptCount { get; set; }
	public int DeliveredCount { get; set; }
	public int FailedCount { get; set; }
	public decimal EstimatedCost { get; set; }
	public string CurrencyCode { get; set; } = "INR";

	protected UsageLedgerEntry()
	{
	}

	public UsageLedgerEntry(Guid id, Guid? tenantId) : base(id)
	{
		TenantId = tenantId;
		UsageDate = DateOnly.FromDateTime(DateTime.UtcNow);
	}
}
