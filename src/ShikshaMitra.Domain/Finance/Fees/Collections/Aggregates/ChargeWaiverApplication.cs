using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>Immutable evidence that the then-current remaining balance of an approved charge was waived.</summary>
public class ChargeWaiverApplication : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid ChargeId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public decimal ChargeAmount { get; private set; }
	public decimal EffectiveAmount { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public Guid AppliedByUserId { get; private set; }
	public string IdempotencyKey { get; private set; } = string.Empty;

	protected ChargeWaiverApplication() { }

	public ChargeWaiverApplication(Guid id, Guid? tenantId, Guid studentId, Guid chargeId,
		Guid academicYearId, decimal chargeAmount, decimal effectiveAmount, string reason,
		Guid appliedByUserId, string idempotencyKey) : base(id)
	{
		if (string.IsNullOrWhiteSpace(reason))
			throw new BusinessException(FeeWaiverErrorCodes.ReasonRequired);
		if (effectiveAmount <= 0)
			throw new BusinessException(FeeWaiverErrorCodes.NoRemainingBalance);
		if (string.IsNullOrWhiteSpace(idempotencyKey))
			throw new BusinessException(FeeWaiverErrorCodes.IdempotencyKeyRequired);

		TenantId = tenantId;
		StudentId = studentId;
		ChargeId = chargeId;
		AcademicYearId = academicYearId;
		ChargeAmount = chargeAmount;
		EffectiveAmount = effectiveAmount;
		Reason = reason.Trim();
		AppliedByUserId = appliedByUserId;
		IdempotencyKey = idempotencyKey.Trim();
	}
}
