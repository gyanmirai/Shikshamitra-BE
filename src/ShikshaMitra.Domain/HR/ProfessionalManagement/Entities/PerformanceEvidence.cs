using System;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class PerformanceEvidence : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReviewAssignmentId { get; private set; }
	public Guid? CriterionId { get; private set; }
	public string Title { get; private set; } = string.Empty;
	public string? FileName { get; private set; }
	public string? Url { get; private set; }
	public string? Notes { get; private set; }

	protected PerformanceEvidence()
	{
	}

	public PerformanceEvidence(Guid id, Guid? tenantId, Guid reviewAssignmentId, Guid? criterionId, string title, string? fileName, string? url, string? notes) : base(id)
	{
		TenantId = tenantId;
		ReviewAssignmentId = reviewAssignmentId;
		CriterionId = criterionId;
		Title = title.Trim();
		FileName = string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim();
		Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
		Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
	}
}
