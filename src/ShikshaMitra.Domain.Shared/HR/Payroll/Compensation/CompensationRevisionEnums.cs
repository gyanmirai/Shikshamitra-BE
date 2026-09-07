namespace ShikshaMitra.HR.Payroll.Compensation;

public enum CompensationRevisionStatus
{
	Draft = 0,
	PendingApproval = 1,
	Approved = 2,
	Rejected = 3,
	Superseded = 4
}

public enum CompensationLineSource
{
	Template = 0,
	Manual = 1
}
