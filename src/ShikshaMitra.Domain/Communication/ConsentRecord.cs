using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication;

public class ConsentRecord : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public string RecipientType { get; set; } = string.Empty;
	public Guid? RecipientId { get; set; }
	public string DisplayName { get; set; } = string.Empty;
	public CommunicationChannelType Channel { get; set; }
	public string Destination { get; set; } = string.Empty;
	public CommunicationConsentStatus Status { get; set; }
	public string Source { get; set; } = "Manual";
	public DateTimeOffset? ConsentCapturedAt { get; set; }
	public DateTimeOffset? RevokedAt { get; set; }
	public string? Notes { get; set; }

	protected ConsentRecord()
	{
	}

	public ConsentRecord(Guid id, Guid? tenantId) : base(id)
	{
		TenantId = tenantId;
		Status = CommunicationConsentStatus.Unknown;
	}
}
