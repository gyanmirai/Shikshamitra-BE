using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Finance.ConcessionRequests;

/// <summary>
/// Step 4 — stores discount values per fee head per concession type.
/// </summary>
public class ConcessionRequestFeeHeadValue : Entity<Guid>
{
	public Guid ConcessionRequestId { get; private set; }
	public Guid ConcessionTypeId { get; private set; }

	public Guid ConcessionRequestConcessionTypeId { get; private set; }
	public Guid FeeHeadId { get; private set; }

	public DiscountBasedOn BasedOn { get; private set; }

	/// <summary>Submitted discount value (flat INR or percentage).</summary>
	public double Amount { get; private set; }

	/// <summary>Server-computed INR discount amount.</summary>
	public double DiscountAmount { get; private set; }

	/// <summary>Net payable = annualBase − discountAmount.</summary>
	public double NetAmount { get; private set; }

	protected ConcessionRequestFeeHeadValue() { }

	public ConcessionRequestFeeHeadValue(
		Guid id,
		Guid concessionRequestId,
		Guid concessionTypeId,
		Guid concessionRequestConcessionTypeId,
		Guid feeHeadId,
		DiscountBasedOn basedOn,
		double amount,
		double discountAmount,
		double netAmount) : base(id)
	{
		ConcessionRequestId = concessionRequestId;
		ConcessionTypeId = concessionTypeId;
		ConcessionRequestConcessionTypeId = concessionRequestConcessionTypeId;
		FeeHeadId = feeHeadId;
		BasedOn = basedOn;
		Amount = amount;
		DiscountAmount = discountAmount;
		NetAmount = netAmount;
	}
}
