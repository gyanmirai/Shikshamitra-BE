using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication;

public class DeliveryAttempt : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid CampaignId { get; set; }
	public Guid? RecipientSnapshotId { get; set; }
	public CommunicationChannelType Channel { get; set; }
	public string Destination { get; set; } = string.Empty;
	public CommunicationDeliveryStatus Status { get; set; }
	public string? ProviderName { get; set; }
	public string? ProviderMessageId { get; set; }
	public int AttemptNumber { get; set; } = 1;
	public string? ErrorCode { get; set; }
	public string? ErrorMessage { get; set; }
	public DateTimeOffset? QueuedAt { get; set; }
	public DateTimeOffset? SentAt { get; set; }
	public DateTimeOffset? DeliveredAt { get; set; }
	public DateTimeOffset? ReadAt { get; set; }
	public decimal? EstimatedCost { get; set; }
	public string CurrencyCode { get; set; } = "INR";

	protected DeliveryAttempt()
	{
	}

	public DeliveryAttempt(Guid id, Guid? tenantId) : base(id)
	{
		TenantId = tenantId;
		Status = CommunicationDeliveryStatus.Queued;
	}
}
