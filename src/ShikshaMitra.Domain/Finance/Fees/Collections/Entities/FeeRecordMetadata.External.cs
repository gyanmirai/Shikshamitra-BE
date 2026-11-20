using System;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Collections;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public partial class FeeRecordMetadata
{
	private FeeRecordMetadata(Guid id) : base(id) { }
	public static FeeRecordMetadata CreateExternal(Guid id, Guid tenantId, FinanceExternalPayerIdentity payer,
		Guid yearId, decimal amount, string currency, string number, FeeCollectionStatus status,
		string key, string fingerprint, string? remarks)
	{
		FinanceMoneyPrecision.Ensure(amount, positive: true);
		FinanceFeePayerIdentity.Validate(payer.StudentId, null, payer.EmployeeId);
		if (id == Guid.Empty || tenantId == Guid.Empty || yearId == Guid.Empty || currency != "INR" ||
			string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(key) || key.Length > 100 || fingerprint.Length != 64)
			throw new BusinessException(FeeCollectionErrorCodes.PaymentDetailsMismatch);
		return new(id) { TenantId = tenantId, StudentId = payer.StudentId, EmployeeId = payer.EmployeeId,
			AcademicYearId = yearId, TotalPaidFee = amount, Currency = currency, ReceiptNumber = number,
			Status = status, IdempotencyKey = key, CommandFingerprint = fingerprint, Remarks = remarks };
	}
}
