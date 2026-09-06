namespace ShikshaMitra.Domain.Finance.Policies;

public enum ConcessionPolicyStatus
{
	Draft = 0,
	Active = 1,
	Archived = 2
}

public enum ConcessionValueSource
{
	PolicyFixed = 0,
	RequestEnteredWithinLimits = 1
}

public enum ConcessionCalculationMethod
{
	Percentage = 0,
	FixedAmount = 1,
	FullWaiver = 2
}

public enum ConcessionApplyOn
{
	AllOutstanding = 0,
	CurrentInstallment = 1,
	FutureInstallments = 2
}
