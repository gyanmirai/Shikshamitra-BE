using System;
using System.Linq.Expressions;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;

namespace ShikshaMitra.Finance.ExternalReceivables;

public static class FinanceExternalRefundEvidenceExpressions
{
	public static readonly Expression<Func<FinanceExternalRefundEvidenceInput, bool>> Valid = value =>
		value.OwnerLinkId.HasValue && value.RequestLinkId == value.OwnerLinkId && value.OwnerTenant == value.TenantId &&
		value.OwnerKind == FinanceExternalSourceKind.LibraryAssessment && value.OwnerChargeId.HasValue &&
		value.OwnerChargeId == value.ActualChargeId && value.OwnerChargeId == value.ClaimedChargeId &&
		value.ChargeTenant == value.TenantId && value.RequestTenant == value.TenantId &&
		value.OwnerYear == value.ChargeYear && value.RequestYear == value.ChargeYear &&
		value.RequestStudent == value.ChargeStudent && value.RequestEmployee == value.ChargeEmployee &&
		!value.RequestAccount.HasValue && value.RequestCurrency == "INR" &&
		value.LineRequestId == value.RequestId && !value.DueFeeId.HasValue && !value.LateFeeId.HasValue &&
		value.OriginalReceiptRepresented && value.LineAllocationId.HasValue && value.AllocationId == value.LineAllocationId &&
		value.AllocationTenant == value.TenantId && value.AllocationChargeId == value.ClaimedChargeId &&
		value.AllocationStudent == value.ChargeStudent && value.AllocationEmployee == value.ChargeEmployee &&
		value.HeaderId.HasValue && value.AllocationHeaderId == value.HeaderId && value.HeaderTenant == value.TenantId &&
		value.HeaderYear == value.ChargeYear && value.HeaderStudent == value.ChargeStudent &&
		value.HeaderEmployee == value.ChargeEmployee && !value.HeaderAccount.HasValue && value.HeaderCurrency == "INR" &&
		(value.HeaderStatus == FeeCollectionStatus.Paid || value.HeaderStatus == FeeCollectionStatus.Partial) &&
		value.HeaderAmount == value.AllocationAmount && value.LineReceiptId.HasValue && value.ReceiptId == value.LineReceiptId &&
		value.ReceiptTenant == value.TenantId && value.ReceiptYear == value.ChargeYear &&
		value.ReceiptStudent == value.ChargeStudent && value.ReceiptEmployee == value.ChargeEmployee &&
		!value.ReceiptAccount.HasValue && value.ReceiptHeaderId == value.HeaderId && value.ReceiptCurrency == "INR" &&
		value.ReceiptAmount == value.HeaderAmount && value.ReceiptScope == FeeReceiptScope.Charge &&
		(value.ReceiptKind == FeeReceiptKind.FinalFee || value.ReceiptKind == FeeReceiptKind.FinalCharge ||
		 (value.ReceiptKind == FeeReceiptKind.ProvisionalFee || value.ReceiptKind == FeeReceiptKind.ProvisionalCharge) &&
		 value.ReceiptVerification == FeeReceiptVerificationStatus.Verified) &&
		// Validate each line before summation; corrupt signed siblings cannot cancel.
		value.AllocationAmount > 0m && value.AllocationAmount <= FinanceChargeAmountRules.MaximumAmount &&
		value.SettledPaid >= 0m && value.SettledPaid <= value.AllocationAmount &&
		value.Requested > 0m && value.Requested <= value.SettledPaid &&
		value.Calculated >= 0m && value.Calculated <= value.Requested &&
		value.Approved >= 0m && value.Approved <= value.Requested &&
		value.Processed >= 0m && value.Processed <= value.Approved &&
		value.Reversed >= 0m && value.Reversed <= value.Processed;
	private static readonly Func<FinanceExternalRefundEvidenceInput, bool> ValidValue = Valid.Compile();
	public static bool IsValid(FinanceExternalRefundEvidenceInput value) => ValidValue(value);
}
