using System;
using ShikshaMitra.HR.Payroll.Statutory;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class StatutoryRuleVersion : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string SchemeCode { get; private set; } = string.Empty;
	public string JurisdictionCode { get; private set; } = string.Empty;
	public int Version { get; private set; }
	public DateTime EffectiveFrom { get; private set; }
	public DateTime? EffectiveTo { get; private set; }
	public int EstablishmentThreshold { get; private set; }
	public decimal StandardWageCeiling { get; private set; }
	public decimal DisabilityWageCeiling { get; private set; }
	public decimal EmployeePercentage { get; private set; }
	public decimal EmployerPercentage { get; private set; }
	public decimal ExclusionCapPercentage { get; private set; }
	public decimal InKindCapPercentage { get; private set; }
	public StatutoryRoundingPolicy RoundingPolicy { get; private set; }
	public string SourceUrl { get; private set; } = string.Empty;
	public DateTime SourceVerifiedOn { get; private set; }
	public bool IsActive { get; private set; }

	protected StatutoryRuleVersion() { }

	public StatutoryRuleVersion(
		Guid id,
		Guid? tenantId,
		string schemeCode,
		string jurisdictionCode,
		int version,
		DateTime effectiveFrom,
		DateTime? effectiveTo,
		int establishmentThreshold,
		decimal standardWageCeiling,
		decimal disabilityWageCeiling,
		decimal employeePercentage,
		decimal employerPercentage,
		decimal exclusionCapPercentage,
		decimal inKindCapPercentage,
		StatutoryRoundingPolicy roundingPolicy,
		string sourceUrl,
		DateTime sourceVerifiedOn,
		bool isActive = true) : base(id)
	{
		TenantId = tenantId;
		SchemeCode = schemeCode.Trim().ToUpperInvariant();
		JurisdictionCode = jurisdictionCode.Trim().ToUpperInvariant();
		Version = version;
		EffectiveFrom = effectiveFrom.Date;
		EffectiveTo = effectiveTo?.Date;
		EstablishmentThreshold = establishmentThreshold;
		StandardWageCeiling = standardWageCeiling;
		DisabilityWageCeiling = disabilityWageCeiling;
		EmployeePercentage = employeePercentage;
		EmployerPercentage = employerPercentage;
		ExclusionCapPercentage = exclusionCapPercentage;
		InKindCapPercentage = inKindCapPercentage;
		RoundingPolicy = roundingPolicy;
		SourceUrl = sourceUrl.Trim();
		SourceVerifiedOn = sourceVerifiedOn.Date;
		IsActive = isActive;
	}
}
