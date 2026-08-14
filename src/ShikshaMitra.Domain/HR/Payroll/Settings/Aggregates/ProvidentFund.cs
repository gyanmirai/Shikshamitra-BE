using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Settings;

public class ProvidentFund : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get;  private set; }
	public string Code { get; private set; } = string.Empty;
	public string Name { get; private set; } = string.Empty;
	public decimal EmployeePercentage { get; private set; }
	public decimal EmployerPercentage { get; private set; }
	public PfContributionBasis ContributionBasis { get; private set; }
	public decimal? WageCeiling { get; private set; }
	public DateTime EffectiveFrom { get; private set; }
	public DateTime? EffectiveTo { get; private set; }
	public bool IsActive { get; private set; }
	public int Version { get; private set; }

	protected ProvidentFund() { } // For ORM

	public ProvidentFund(
		Guid id,
		Guid? tenantId,
		string code,
		string name,
		decimal employeePercentage,
		decimal employerPercentage,
		PfContributionBasis contributionBasis = PfContributionBasis.Basic,
		decimal? wageCeiling = null,
		DateTime? effectiveFrom = null,
		DateTime? effectiveTo = null,
		bool isActive = true) : base(id)
	{
		TenantId = tenantId;
		Code = code.Trim();
		Name = name.Trim();
		EmployeePercentage = employeePercentage;
		EmployerPercentage = employerPercentage;
		ContributionBasis = contributionBasis;
		WageCeiling = wageCeiling;
		EffectiveFrom = (effectiveFrom ?? new DateTime(2022, 4, 1)).Date;
		EffectiveTo = effectiveTo?.Date;
		IsActive = isActive;
		Version = 1;
	}

	public void Update(
		string code,
		string name,
		decimal employeePercentage,
		decimal employerPercentage,
		PfContributionBasis contributionBasis,
		decimal? wageCeiling,
		DateTime effectiveFrom,
		DateTime? effectiveTo,
		bool isActive)
	{
		Code = code.Trim();
		Name = name.Trim();
		EmployeePercentage = employeePercentage;
		EmployerPercentage = employerPercentage;
		ContributionBasis = contributionBasis;
		WageCeiling = wageCeiling;
		EffectiveFrom = effectiveFrom.Date;
		EffectiveTo = effectiveTo?.Date;
		IsActive = isActive;
		Version++;
	}
}
