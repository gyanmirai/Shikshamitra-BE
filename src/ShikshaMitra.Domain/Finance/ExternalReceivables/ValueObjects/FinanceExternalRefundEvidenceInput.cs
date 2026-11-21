using System;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed class FinanceExternalRefundEvidenceInput
{
	public Guid TenantId { get; init; }
	public Guid RequestId { get; init; }
	public Guid? RequestTenant { get; init; }
	public Guid RequestYear { get; init; }
	public Guid? RequestStudent { get; init; }
	public Guid? RequestEmployee { get; init; }
	public Guid? RequestAccount { get; init; }
	public string? RequestCurrency { get; init; }
	public Guid? RequestLinkId { get; init; }
	public bool OriginalReceiptRepresented { get; init; }
	public Guid? OwnerLinkId { get; init; }
	public Guid? OwnerTenant { get; init; }
	public Guid? OwnerChargeId { get; init; }
	public Guid? OwnerYear { get; init; }
	public FinanceExternalSourceKind? OwnerKind { get; init; }
	public Guid? ActualChargeId { get; init; }
	public Guid? ChargeTenant { get; init; }
	public Guid? ChargeYear { get; init; }
	public Guid? ChargeStudent { get; init; }
	public Guid? ChargeEmployee { get; init; }
	public Guid LineRequestId { get; init; }
	public Guid? ClaimedChargeId { get; init; }
	public Guid? DueFeeId { get; init; }
	public Guid? LateFeeId { get; init; }
	public decimal Requested { get; init; }
	public decimal Calculated { get; init; }
	public decimal Approved { get; init; }
	public decimal Processed { get; init; }
	public decimal Reversed { get; init; }
	public decimal SettledPaid { get; init; }
	public Guid? LineAllocationId { get; init; }
	public Guid? AllocationId { get; init; }
	public Guid? AllocationTenant { get; init; }
	public Guid? AllocationChargeId { get; init; }
	public Guid? AllocationStudent { get; init; }
	public Guid? AllocationEmployee { get; init; }
	public Guid? AllocationHeaderId { get; init; }
	public decimal? AllocationAmount { get; init; }
	public Guid? HeaderId { get; init; }
	public Guid? HeaderTenant { get; init; }
	public Guid? HeaderYear { get; init; }
	public Guid? HeaderStudent { get; init; }
	public Guid? HeaderEmployee { get; init; }
	public Guid? HeaderAccount { get; init; }
	public string? HeaderCurrency { get; init; }
	public decimal? HeaderAmount { get; init; }
	public FeeCollectionStatus? HeaderStatus { get; init; }
	public Guid? LineReceiptId { get; init; }
	public Guid? ReceiptId { get; init; }
	public Guid? ReceiptTenant { get; init; }
	public Guid? ReceiptYear { get; init; }
	public Guid? ReceiptStudent { get; init; }
	public Guid? ReceiptEmployee { get; init; }
	public Guid? ReceiptAccount { get; init; }
	public Guid? ReceiptHeaderId { get; init; }
	public string? ReceiptCurrency { get; init; }
	public decimal? ReceiptAmount { get; init; }
	public FeeReceiptKind? ReceiptKind { get; init; }
	public FeeReceiptScope? ReceiptScope { get; init; }
	public FeeReceiptVerificationStatus? ReceiptVerification { get; init; }
}
