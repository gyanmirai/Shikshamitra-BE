using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Settings;

public class ProfessionalTax : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string JurisdictionCode { get; private set; } = "IN-GJ";
	public DateTime EffectiveFrom { get; private set; }
	public DateTime? EffectiveTo { get; private set; }
	public decimal FromSalary { get; private set; }
	public decimal? ToSalary { get; private set; }
	public decimal TaxAmount { get; private set; }
	public bool IsActive { get; private set; }
	public bool DeductInCessationMonth { get; private set; }
	public int Version { get; private set; }

	protected ProfessionalTax()
	{
	}

	public ProfessionalTax(
		Guid id,
		Guid? tenantId,
		string jurisdictionCode,
		DateTime effectiveFrom,
		DateTime? effectiveTo,
		decimal fromSalary,
		decimal? toSalary,
		decimal taxAmount,
		bool isActive = true,
		bool deductInCessationMonth = true) : base(id)
	{
		TenantId = tenantId;
		JurisdictionCode = jurisdictionCode.Trim().ToUpperInvariant();
		EffectiveFrom = effectiveFrom.Date;
		EffectiveTo = effectiveTo?.Date;
		FromSalary = fromSalary;
		ToSalary = toSalary;
		TaxAmount = taxAmount;
		IsActive = isActive;
		DeductInCessationMonth = deductInCessationMonth;
		Version = 1;
	}

	public void Update(
		string jurisdictionCode,
		DateTime effectiveFrom,
		DateTime? effectiveTo,
		decimal fromSalary,
		decimal? toSalary,
		decimal taxAmount,
		bool isActive,
		bool deductInCessationMonth)
	{
		JurisdictionCode = jurisdictionCode.Trim().ToUpperInvariant();
		EffectiveFrom = effectiveFrom.Date;
		EffectiveTo = effectiveTo?.Date;
		FromSalary = fromSalary;
		ToSalary = toSalary;
		TaxAmount = taxAmount;
		IsActive = isActive;
		DeductInCessationMonth = deductInCessationMonth;
		Version++;
	}
}
