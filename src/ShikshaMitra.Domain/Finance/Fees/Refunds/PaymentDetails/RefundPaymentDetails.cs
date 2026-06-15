using System.Text.Json.Serialization;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

/// <summary>
/// Abstract base value object for refund-payment-method-specific details.
/// Mirrors <c>PaymentDetails</c> in the fee collection context, but captures
/// the school-side disbursement information (who receives, source account, etc.)
/// rather than the student-side payment instrument.
///
/// Stored as a <c>jsonb</c> column in <c>Finance_FeeRefundRequests</c> via
/// a value converter.  The <see cref="JsonDerivedTypeAttribute"/> attributes
/// enable polymorphic System.Text.Json deserialization through the "$type"
/// discriminator without a custom converter.
/// </summary>
[JsonDerivedType(typeof(CashRefundDetails),         typeDiscriminator: nameof(CashRefundDetails))]
[JsonDerivedType(typeof(ChequeRefundDetails),       typeDiscriminator: nameof(ChequeRefundDetails))]
[JsonDerivedType(typeof(DemandDraftRefundDetails),  typeDiscriminator: nameof(DemandDraftRefundDetails))]
[JsonDerivedType(typeof(BankTransferRefundDetails), typeDiscriminator: nameof(BankTransferRefundDetails))]
[JsonDerivedType(typeof(UpiRefundDetails),          typeDiscriminator: nameof(UpiRefundDetails))]
public abstract class RefundPaymentDetails : ValueObject
{
    /// <summary>Optional free-text note attached to the refund disbursement.</summary>
    public string? Remarks { get; protected set; }
}
