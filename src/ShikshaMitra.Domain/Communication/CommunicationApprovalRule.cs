using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication;

public class CommunicationApprovalRule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public string RuleName { get; set; } = string.Empty;
	public bool IsEnabled { get; set; } = true;
	public string? CampaignType { get; set; }
	public CommunicationSourceModule? SourceModule { get; set; }
	public CommunicationChannelType? Channel { get; set; }
	public int? MinimumAudienceSize { get; set; }
	public CommunicationImportanceLevel? ImportanceLevel { get; set; }
	public bool RequiresApproval { get; set; } = true;

	protected CommunicationApprovalRule()
	{
	}

	public CommunicationApprovalRule(Guid id, Guid? tenantId) : base(id)
	{
		TenantId = tenantId;
	}
}
