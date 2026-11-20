using System;
using Volo.Abp;
using ShikshaMitra.Finance.ExternalReceivables;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>Immutable evidence that the then-current remaining balance of an approved charge was waived.</summary>
public class ChargeWaiverApplication : BasicAggregateRoot<Guid>, IMultiTenant, ICreationAuditedObject
{
	public DateTime CreationTime { get; private set; }
	public Guid? CreatorId { get; private set; }
	public string LegacyConcurrencyStamp { get; private set; } = Guid.NewGuid().ToString("N");
	public string LegacyExtraPropertiesJson { get; private set; } = "{}";
	public Guid? TenantId { get; private set; }
	public Guid? StudentId { get; private set; }
	public Guid? EmployeeId { get; private set; }
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
	public static ChargeWaiverApplication CreateExternal(Guid id, Guid tenantId, FinanceExternalPayerIdentity payer,
		Guid chargeId, Guid yearId, decimal originalAmount, decimal amount, string reason, Guid actorId, string key)
	{
		FinanceExternalSettlementAmounts.EnsureMoney(amount, positive: true);
		FinanceFeePayerIdentity.Validate(payer.StudentId, null, payer.EmployeeId);
		if (id == Guid.Empty || tenantId == Guid.Empty || chargeId == Guid.Empty || yearId == Guid.Empty || actorId == Guid.Empty ||
			amount > originalAmount || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000 || string.IsNullOrWhiteSpace(key))
			throw new BusinessException(FeeWaiverErrorCodes.SourceNotEligible);
		return new ChargeWaiverApplication
		{
			Id = id, TenantId = tenantId, StudentId = payer.StudentId, EmployeeId = payer.EmployeeId,
			ChargeId = chargeId, AcademicYearId = yearId, ChargeAmount = originalAmount, EffectiveAmount = amount,
			Reason = reason.Trim(), AppliedByUserId = actorId, IdempotencyKey = key
		};
	}
}
