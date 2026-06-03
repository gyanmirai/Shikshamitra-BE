using System;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class PerformanceFeedback : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReviewAssignmentId { get; private set; }
	public PerformanceFeedbackSource Source { get; private set; }
	public string? ProviderName { get; private set; }
	public string FeedbackText { get; private set; } = string.Empty;

	protected PerformanceFeedback()
	{
	}

	public PerformanceFeedback(Guid id, Guid? tenantId, Guid reviewAssignmentId, PerformanceFeedbackSource source, string? providerName, string feedbackText) : base(id)
	{
		TenantId = tenantId;
		ReviewAssignmentId = reviewAssignmentId;
		Source = source;
		ProviderName = string.IsNullOrWhiteSpace(providerName) ? null : providerName.Trim();
		FeedbackText = feedbackText.Trim();
	}
}
