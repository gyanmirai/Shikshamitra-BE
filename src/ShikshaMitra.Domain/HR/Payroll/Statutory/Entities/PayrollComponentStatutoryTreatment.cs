using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class PayrollComponentStatutoryTreatment : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ComponentId { get; private set; }
	public string SchemeCode { get; private set; } = string.Empty;
	public StatutoryWageTreatment Treatment { get; private set; }
	public DateTime EffectiveFrom { get; private set; }
	public DateTime? EffectiveTo { get; private set; }

	protected PayrollComponentStatutoryTreatment() { }

	public PayrollComponentStatutoryTreatment(
		Guid id,
		Guid? tenantId,
		Guid componentId,
		string schemeCode,
		StatutoryWageTreatment treatment,
		DateTime effectiveFrom,
		DateTime? effectiveTo = null) : base(id)
	{
		TenantId = tenantId;
		ComponentId = componentId;
		SchemeCode = schemeCode.Trim().ToUpperInvariant();
		Treatment = treatment;
		EffectiveFrom = effectiveFrom.Date;
		EffectiveTo = effectiveTo?.Date;
	}

	public void Update(StatutoryWageTreatment treatment, DateTime effectiveFrom, DateTime? effectiveTo)
	{
		Treatment = treatment;
		EffectiveFrom = effectiveFrom.Date;
		EffectiveTo = effectiveTo?.Date;
	}
}
