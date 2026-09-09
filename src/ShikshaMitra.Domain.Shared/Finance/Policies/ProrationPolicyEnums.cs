namespace ShikshaMitra.Domain.Finance.Policies;

public enum ProrationPolicyStatus
{
	Draft = 0,
	Active = 1,
	Archived = 2
}

public enum ProrationRuleType
{
	FullCharge = 0,
	Daily = 1,
	MonthlyCutoff = 2
}

public enum ProrationRoundingMode
{
	Currency = 0,
	NearestRupee = 1,
	RoundUpRupee = 2,
	RoundDownRupee = 3
}

public enum ProrationAfterCutoffAction
{
	NoCharge = 0,
	HalfCharge = 1
}

public enum ChargePeriodSource
{
	AcademicYear = 0,
	Admission = 1,
	StudentAssignment = 2,
	HostelService = 3,
	TransportService = 4,
	StudentExit = 5,
	ManualOverride = 6
}
