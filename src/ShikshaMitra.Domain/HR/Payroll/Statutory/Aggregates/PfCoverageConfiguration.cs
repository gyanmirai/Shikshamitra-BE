using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class PfCoverageConfiguration : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public DateTime EffectiveMonth { get; private set; }
	public bool IsCovered { get; private set; }
	public bool IsHandledExternally { get; private set; }
	public bool IsCancelled { get; private set; }
	public string RequestKey { get; private set; } = string.Empty;
	public string EvidenceReference { get; private set; } = string.Empty;
	protected PfCoverageConfiguration() { }
	public PfCoverageConfiguration(Guid id, Guid? tenantId, DateTime effectiveMonth, bool covered, string? evidence, string requestKey,
		bool isHandledExternally = false) : base(id)
	{
		var evidenceReference = evidence?.Trim() ?? string.Empty;
		if (effectiveMonth.Day != 1 || effectiveMonth.Year < 2000 ||
			evidenceReference.Length > 1000 || (covered && (evidenceReference.Length == 0 || isHandledExternally)))
			throw new BusinessException("HR:Payroll:PfCoverageConfigurationInvalid");
		TenantId = tenantId;
		EffectiveMonth = effectiveMonth.Date;
		IsCovered = covered;
		IsHandledExternally = isHandledExternally;
		RequestKey = Check.NotNullOrWhiteSpace(requestKey, nameof(requestKey), 32);
		EvidenceReference = evidenceReference;
	}
	public void Cancel() => IsCancelled = true;
}
