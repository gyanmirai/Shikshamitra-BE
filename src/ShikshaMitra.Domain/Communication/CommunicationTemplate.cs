using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication;

public class CommunicationTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid? AcademicMediumId { get; set; }
	public string TemplateName { get; set; } = string.Empty;
	public string? TargetRole { get; set; }
	public string Category { get; set; } = "General";
	public string Subject { get; set; } = string.Empty;
	public string Body { get; set; } = string.Empty;
	public string LanguageCode { get; set; } = "en";
	public string ChannelsJson { get; set; } = "[]";
	public string VariablesJson { get; set; } = "[]";
	public string HashtagsJson { get; set; } = "[]";
	public CommunicationTemplateApprovalStatus ApprovalStatus { get; set; }
	public int Version { get; set; } = 1;
	public bool IsActive { get; set; } = true;

	protected CommunicationTemplate()
	{
	}

	public CommunicationTemplate(Guid id, Guid? tenantId) : base(id)
	{
		TenantId = tenantId;
		ApprovalStatus = CommunicationTemplateApprovalStatus.Draft;
	}
}
