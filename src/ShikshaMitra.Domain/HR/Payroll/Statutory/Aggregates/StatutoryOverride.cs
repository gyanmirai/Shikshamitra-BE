using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class StatutoryOverride : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public string SchemeCode { get; private set; } = string.Empty;
	public StatutoryOverrideType OverrideType { get; private set; }
	public decimal? Amount { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public string? EvidenceReference { get; private set; }
	public DateTime EffectiveFrom { get; private set; }
	public DateTime EffectiveTo { get; private set; }
	public Guid? ApprovedByUserId { get; private set; }

	protected StatutoryOverride() { }

	public StatutoryOverride(
		Guid id,
		Guid? tenantId,
		Guid employeeId,
		string schemeCode,
		StatutoryOverrideType overrideType,
		decimal? amount,
		string reason,
		string? evidenceReference,
		DateTime effectiveFrom,
		DateTime effectiveTo,
		Guid? approvedByUserId) : base(id)
	{
		TenantId = tenantId;
		if (string.Equals(schemeCode, "ESI", StringComparison.OrdinalIgnoreCase) &&
			(effectiveFrom.Day != 1 || effectiveTo.Day != DateTime.DaysInMonth(effectiveTo.Year, effectiveTo.Month) || effectiveTo.Date < effectiveFrom.Date))
			throw new BusinessException(HRErrorCodes.StatutoryOverrideInvalid)
				.WithData("reason", "ESIC exceptions must cover complete calendar months.");
		EmployeeId = employeeId;
		SchemeCode = schemeCode.Trim().ToUpperInvariant();
		OverrideType = overrideType;
		Amount = amount;
		Reason = reason.Trim();
		EvidenceReference = string.IsNullOrWhiteSpace(evidenceReference) ? null : evidenceReference.Trim();
		EffectiveFrom = effectiveFrom.Date;
		EffectiveTo = effectiveTo.Date;
		ApprovedByUserId = approvedByUserId;
	}

	public bool AppliesOn(DateTime date) => date.Date >= EffectiveFrom && date.Date <= EffectiveTo;
}