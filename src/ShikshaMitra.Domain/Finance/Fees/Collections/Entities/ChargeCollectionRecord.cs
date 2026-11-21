using System;
using Volo.Abp.Domain.Entities.Auditing;
using ShikshaMitra.Finance.ExternalReceivables;
using Volo.Abp;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public class ChargeCollectionRecord : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	private ChargeCollectionRecord() { /* For ORM */ }

	public ChargeCollectionRecord(Guid id) : base(id) { }

	public Guid? TenantId { get; set; }
	public Guid? StudentId { get; set; }
	public Guid? EmployeeId { get; private set; }
	public Guid ChargeId { get; set; }
	public decimal Amount { get; set; }
	public DateTimeOffset PaymentDate { get; set; }
	public Guid FeeRecordMetadataId { get; set; }
	public static ChargeCollectionRecord CreateExternal(Guid id, Guid tenantId, Guid chargeId, Guid metadataId,
		FinanceExternalPayerIdentity payer, decimal amount, DateTimeOffset at)
	{
		FinanceMoneyPrecision.Ensure(amount, positive: true);
		FinanceFeePayerIdentity.Validate(payer.StudentId, null, payer.EmployeeId);
		if (id == Guid.Empty || tenantId == Guid.Empty || chargeId == Guid.Empty || metadataId == Guid.Empty || at == default)
			throw new BusinessException(ShikshaMitra.Finance.Fees.Collections.FeeCollectionErrorCodes.InvalidCharge);
		return new(id) { TenantId = tenantId, ChargeId = chargeId, FeeRecordMetadataId = metadataId,
			StudentId = payer.StudentId, EmployeeId = payer.EmployeeId, Amount = amount, PaymentDate = at };
	}
}
