using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication;

public class ChannelProviderSetting : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public CommunicationChannelType Channel { get; set; }
	public string ProviderName { get; set; } = string.Empty;
	public string? SenderId { get; set; }
	public bool IsEnabled { get; set; }
	public bool IsDefault { get; set; }
	public string? ConfigurationJson { get; set; }
	public string? SecretReference { get; set; }
	public string FallbackOrderJson { get; set; } = "[]";
	public int? DailyQuota { get; set; }
	public int? MonthlyQuota { get; set; }
	public bool ConsentRequired { get; set; } = true;
	public bool TemplateApprovalRequired { get; set; }
	public CommunicationProviderStatus Status { get; set; }

	protected ChannelProviderSetting()
	{
	}

	public ChannelProviderSetting(Guid id, Guid? tenantId) : base(id)
	{
		TenantId = tenantId;
		Status = CommunicationProviderStatus.Disabled;
	}
}
