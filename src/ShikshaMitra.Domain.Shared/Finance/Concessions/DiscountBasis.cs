namespace ShikshaMitra.Finance.Fees.Concessions;

/// <summary>0 = flat amount, 1 = percentage of fee-head base (matches FE value entry).</summary>
public enum DiscountBasis
{
	FlatAmount = 0,
	Percentage = 1,
}
