using System;
using ShikshaMitra.Domain.Finance.Policies;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace ShikshaMitra.Finance.Fees.Receipts;

/// <summary>
/// Domain manager responsible for creating <see cref="FeePaymentReceipt"/> aggregates.
/// One receipt is created per <see cref="FeeReceiptScope"/> per collection event.
/// The effective collection policy can combine fee and charge lines into one document.
/// <list type="bullet">
///   <item><see cref="FeeReceiptKind.FinalFee"/> → <c>FIN-{yyyy}-{nnnnnn}</c></item>
///   <item><see cref="FeeReceiptKind.ProvisionalFee"/> → <c>KFE-{yyyy}-{nnnnnn}</c></item>
///   <item><see cref="FeeReceiptKind.ProvisionalCharge"/> → <c>KCH-{yyyy}-{nnnnnn}</c></item>
///   <item><see cref="FeeReceiptKind.FinalCharge"/> → <c>FCH-{yyyy}-{nnnnnn}</c></item>
/// </list>
/// </summary>
public class FeeReceiptManager(
	IReceiptNumberSequenceRepository sequenceRepository,
	IClock clock,
	ICurrentTenant currentTenant
) : DomainService
{
	public async Task<string> GenerateReceiptNumberAsync(FeeReceiptKind kind)
	{
		var year = clock.Now.Year;
		var sequence = await sequenceRepository.IncrementAndGetAsync(year, kind);
		var prefix = kind switch
		{
			FeeReceiptKind.FinalFee          => "FIN",
			FeeReceiptKind.ProvisionalFee    => "KFE",
			FeeReceiptKind.ProvisionalCharge => "KCH",
			FeeReceiptKind.FinalCharge       => "FCH",
			_                               => "REC",
		};
		return $"{prefix}-{year}-{sequence:D6}";
	}

	public FeePaymentReceipt CreateReceipt(
		Guid feeCollectionTransactionId,
		Guid? studentId,
		Guid academicYearId,
		Guid boardId,
		Guid educationMediumId,
		FeeReceiptScope scope,
		FeeReceiptKind kind,
		double totalPaidAmount,
		string receiptNumber,
		string? remarks = null,
		Guid? receiptBookId = null,
		Guid? financeFeeAccountId = null)
	{
		FinanceFeePayerIdentity.Validate(studentId, financeFeeAccountId);
		var verificationStatus = kind is FeeReceiptKind.ProvisionalFee or FeeReceiptKind.ProvisionalCharge
			? FeeReceiptVerificationStatus.PendingVerification
			: FeeReceiptVerificationStatus.NotApplicable;

		var receipt = new FeePaymentReceipt(GuidGenerator.Create())
		{
			TenantId                   = currentTenant.Id,
			FeeCollectionTransactionId = feeCollectionTransactionId,
			StudentId                  = studentId,
			AcademicYearId             = academicYearId,
			BoardId                    = boardId,
			EducationMediumId          = educationMediumId,
			ReceiptScope               = scope,
			ReceiptKind                = kind,
			VerificationStatus         = verificationStatus,
			TotalPaidAmount            = totalPaidAmount,
			ReceiptNumber              = receiptNumber,
			ReceiptDate                = DateOnly.FromDateTime(clock.Now.Date),
			Remarks                    = remarks,
			ReceiptBookId              = receiptBookId,
		};
		if (financeFeeAccountId.HasValue) receipt.SetFinanceFeeAccount(financeFeeAccountId.Value);
		return receipt;
	}

	/// <summary>
	/// Creates 1–2 receipts for a single collection transaction.
	/// Both fee and charge receipts follow the same payment-method policy:
	/// Cheque/DD and unconfirmed noncash payments produce provisional documents.
	/// </summary>
	public async Task<List<FeePaymentReceipt>> CreateReceiptsForTransactionAsync(
		Guid feeCollectionTransactionId,
		Guid? studentId,
		Guid academicYearId,
		Guid boardId,
		Guid educationMediumId,
		double feeLinesTotal,
		double chargeLinesTotal,
		IEnumerable<PaymentMethodEnum> paymentMethods,
		bool? bankReceiptConfirmed = null,
		string? remarks = null,
		Guid? financeFeeAccountId = null,
		CollectionReceiptGrouping receiptGrouping = CollectionReceiptGrouping.Separate)
	{
		var receipts = new List<FeePaymentReceipt>();
		if (!Enum.IsDefined(receiptGrouping))
			throw new BusinessException(CollectionPolicyErrorCodes.InvalidReceiptGrouping);
		var methods = paymentMethods.ToList();

		var feeKind = FeeReceiptPaymentPolicy.DetermineFeeReceiptKind(methods, bankReceiptConfirmed);
		if (receiptGrouping == CollectionReceiptGrouping.Combined && feeLinesTotal > 0 && chargeLinesTotal > 0)
		{
			var number = await GenerateReceiptNumberAsync(feeKind);
			var receipt = CreateReceipt(feeCollectionTransactionId, studentId, academicYearId, boardId, educationMediumId,
				FeeReceiptScope.Combined, feeKind, (double)((decimal)feeLinesTotal + (decimal)chargeLinesTotal), number, remarks, financeFeeAccountId: financeFeeAccountId);
			receipt.SetAmountBreakdown((decimal)feeLinesTotal, (decimal)chargeLinesTotal);
			return [receipt];
		}

		if (feeLinesTotal > 0)
		{
			var kind = FeeReceiptPaymentPolicy.DetermineFeeReceiptKind(methods, bankReceiptConfirmed);
			var number = await GenerateReceiptNumberAsync(kind);
			receipts.Add(CreateReceipt(
				feeCollectionTransactionId, studentId, academicYearId, boardId, educationMediumId,
				FeeReceiptScope.Fee, kind, feeLinesTotal, number, remarks, financeFeeAccountId: financeFeeAccountId));
		}

		if (chargeLinesTotal > 0)
		{
			var chargeKind = feeKind == FeeReceiptKind.ProvisionalFee
				? FeeReceiptKind.ProvisionalCharge
				: FeeReceiptKind.FinalCharge;
			var number = await GenerateReceiptNumberAsync(chargeKind);
			receipts.Add(CreateReceipt(
				feeCollectionTransactionId, studentId, academicYearId, boardId, educationMediumId,
				FeeReceiptScope.Charge, chargeKind, chargeLinesTotal, number, remarks, financeFeeAccountId: financeFeeAccountId));
		}

		return receipts;
	}
}
