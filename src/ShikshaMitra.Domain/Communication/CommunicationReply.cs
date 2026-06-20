using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication;

public class CommunicationReply : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid? CampaignId { get; set; }
	public Guid? DeliveryAttemptId { get; set; }
	public Guid? RecipientSnapshotId { get; set; }
	public CommunicationChannelType Channel { get; set; }
	public string FromAddress { get; set; } = string.Empty;
	public string Body { get; set; } = string.Empty;
	public DateTimeOffset ReceivedAt { get; set; }
	public Guid? AssignedToUserId { get; set; }
	public string? AssignedDepartment { get; set; }
	public CommunicationReplyStatus Status { get; set; }
	public string? InternalNotes { get; set; }

	protected CommunicationReply()
	{
	}

	public CommunicationReply(Guid id, Guid? tenantId) : base(id)
	{
		TenantId = tenantId;
		ReceivedAt = DateTimeOffset.UtcNow;
		Status = CommunicationReplyStatus.Open;
	}
}
