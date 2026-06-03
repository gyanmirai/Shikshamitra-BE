using System;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class PerformanceAuditEvent : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? ReviewAssignmentId { get; private set; }
	public string EventType { get; private set; } = string.Empty;
	public string Summary { get; private set; } = string.Empty;
	public string? MetadataJson { get; private set; }

	protected PerformanceAuditEvent()
	{
	}

	public PerformanceAuditEvent(Guid id, Guid? tenantId, Guid? reviewAssignmentId, string eventType, string summary, string? metadataJson = null) : base(id)
	{
		TenantId = tenantId;
		ReviewAssignmentId = reviewAssignmentId;
		EventType = eventType.Trim();
		Summary = summary.Trim();
		MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson.Trim();
	}
}
