using System;
using ShikshaMitra.Finance.Fees.Collections;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// Records a single payment transaction made by a student.
/// Carries polymorphic <see cref="PaymentDetails"/> whose concrete type
/// corresponds to the chosen <see cref="PaymentMethodEnum"/>.
/// </summary>
public class PaymentMethodRecord : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? StudentId { get; private set; }
	public Guid? FinanceFeeAccountId { get; private set; }
	public void LinkStudent(Guid studentId) => StudentId = FinanceFeePayerIdentity.LinkStudent(StudentId, studentId);
 public Guid? BankAccountId { get; private set; }
 public string? BankLabelSnapshot { get; private set; }
 public void SetReceivingBank(Guid? id, string? label) { BankAccountId = id; BankLabelSnapshot = label; }
	public PaymentMethodEnum PaymentMethod { get; private set; }

	/// <summary>Decimal is required for monetary values to avoid floating-point rounding errors.</summary>
	public decimal Amount { get; private set; }

	public Guid FeeRecordMetadataId { get; private set; }
	public string? Notes { get; private set; }
	public DateTimeOffset PaymentDate { get; private set; }

	/// <summary>
	/// Method-specific details. Null only for <see cref="PaymentMethodEnum.Cash"/>,
	/// which requires no additional instrument information.
	/// </summary>
	public PaymentDetails? PaymentDetails { get; private set; }

	private PaymentMethodRecord() { /* For ORM */ }

	/// <summary>
	/// Creates a new <see cref="PaymentMethodRecord"/>.
	/// </summary>
	/// <param name="paymentDetails">
	/// Must be provided for all payment methods except <see cref="PaymentMethodEnum.Cash"/>.
	/// The concrete type must match the <paramref name="paymentMethod"/>.
	/// </param>
	public static PaymentMethodRecord Create(
		Guid? studentId,
		Guid feeRecordMetadataId,
		decimal amount,
		DateTimeOffset paymentDate,
		PaymentMethodEnum paymentMethod,
		PaymentDetails? paymentDetails = null,
		Guid? tenantId = null,
		string? notes = null,
		Guid? financeFeeAccountId = null)
	{
		if (amount <= 0)
			throw new BusinessException(FeeCollectionErrorCodes.PaymentAmountMustBePositive);
		FinanceFeePayerIdentity.Validate(studentId, financeFeeAccountId);

		// Only Cheque, DemandDraft, BankTransfer, and UPI have dedicated detail types
		// and therefore require details. CreditCard, OnlinePaymentGateway, and
		// WalletPrepaidCard do not yet have dedicated types, so details are optional for them.
		var requiresDetails = paymentMethod is
			PaymentMethodEnum.Cheque or
			PaymentMethodEnum.DemandDraft or
			PaymentMethodEnum.BankTransfer or
			PaymentMethodEnum.UPI;

		if (requiresDetails && paymentDetails is null)
			throw new BusinessException(FeeCollectionErrorCodes.PaymentDetailsRequiredForNonCash)
				.WithData("PaymentMethod", paymentMethod);

		ValidateDetailsMatchMethod(paymentMethod, paymentDetails);

		return new PaymentMethodRecord
		{
			Id = Guid.NewGuid(),
			StudentId = studentId,
			FinanceFeeAccountId = financeFeeAccountId,
			FeeRecordMetadataId = feeRecordMetadataId,
			Amount = amount,
			PaymentDate = paymentDate,
			PaymentMethod = paymentMethod,
			PaymentDetails = paymentDetails,
			TenantId = tenantId,
			Notes = notes,
		};
	}

	private static void ValidateDetailsMatchMethod(
		PaymentMethodEnum paymentMethod,
		PaymentDetails? paymentDetails)
	{
		if (paymentDetails is null)
			return;

		var mismatch = paymentMethod switch
		{
			PaymentMethodEnum.Cheque              => paymentDetails is not ChequeDetails,
			PaymentMethodEnum.DemandDraft         => paymentDetails is not DemandDraftDetails,
			PaymentMethodEnum.UPI                 => paymentDetails is not UpiDetails,
			PaymentMethodEnum.BankTransfer        => paymentDetails is not BankTransferDetails,
			PaymentMethodEnum.CreditCard          => paymentDetails is not CreditCardDetails,
			_ => false,
		};

		if (mismatch)
			throw new BusinessException(FeeCollectionErrorCodes.PaymentDetailsMismatch)
				.WithData("PaymentMethod", paymentMethod)
				.WithData("DetailsType", paymentDetails.GetType().Name);
	}
}
