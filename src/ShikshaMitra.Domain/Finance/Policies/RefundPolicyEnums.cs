namespace ShikshaMitra.Domain.Finance.Policies;

public enum RefundPolicyStatus
{
	Draft = 0,
	Active = 1,
	Archived = 2
}

public enum RefundEligiblePaymentState
{
	FullyPaidOnly = 0,
	PartialAndFullPayment = 1
}

public enum RefundFeeHeadScope
{
	AllFeeHeads = 0,
	SelectedFeeHeads = 1
}

public enum RefundAmountMode
{
	RemainingPaidAmount = 0,
	ManualUpToEligible = 1,
	FixedAmount = 2,
	Percentage = 3
}

public enum RefundDeductionType
{
	None = 0,
	FixedAmount = 1,
	Percentage = 2
}

public enum RefundPayoutMethod
{
	Cash = 0,
	Cheque = 1,
	DemandDraft = 2,
	BankTransfer = 3,
	Upi = 4
}
