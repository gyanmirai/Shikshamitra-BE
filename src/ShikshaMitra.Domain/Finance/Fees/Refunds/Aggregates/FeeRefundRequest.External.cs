using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public partial class FeeRefundRequest
{
	internal static FeeRefundRequest CreateExternal(Guid id, Guid tenantId, FinanceExternalPayerIdentity payer,
		Guid academicYearId, Guid sourceLinkId, string voucherNumber, Guid policyId, Guid policyVersionId,
		Guid receiptId, string receiptNumber, IReadOnlyCollection<FeeRefundLine> lines, string method,
		RefundPaymentDetails? paymentDetails, string reason, string policySnapshot, string calculationSnapshot,
		string calculationHash, Guid actorId, string idempotencyKey)
	{
		FinanceFeePayerIdentity.Validate(payer.StudentId, null, payer.EmployeeId);
		if (id == Guid.Empty || tenantId == Guid.Empty || academicYearId == Guid.Empty || sourceLinkId == Guid.Empty ||
			policyId == Guid.Empty || policyVersionId == Guid.Empty || receiptId == Guid.Empty || actorId == Guid.Empty ||
			lines.Count == 0 || lines.Any(line => line.FeeRefundRequestId != id || !line.ChargeId.HasValue ||
				line.DueFeeId.HasValue || line.LateFeeDueFeeId.HasValue || !line.SourceAllocationId.HasValue || !line.SourceReceiptId.HasValue))
			throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
		var request = new FeeRefundRequest
		{
			Id = id, TenantId = tenantId, StudentId = payer.StudentId, EmployeeId = payer.EmployeeId,
			AcademicYearId = academicYearId, FinanceExternalSourceLinkId = sourceLinkId,
			VoucherNumber = Check.NotNullOrWhiteSpace(voucherNumber, nameof(voucherNumber), 50),
			RefundMethod = Normalize(method), RefundPaymentDetails = paymentDetails, Reason = Normalize(reason),
			Status = FeeRefundStatus.Draft
		};
		request.PinPolicy(null, policyId, policyVersionId, receiptId, receiptNumber, policySnapshot, calculationSnapshot, calculationHash);
		foreach (var line in lines) request.AddLine(line);
		request.UseSimpleWorkflow(actorId, idempotencyKey);
		return request;
	}
}
