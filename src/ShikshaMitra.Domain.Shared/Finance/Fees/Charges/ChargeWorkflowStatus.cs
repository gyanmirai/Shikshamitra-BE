namespace ShikshaMitra.Finance.Fees.Charges;

/// <summary>Derived batch lifecycle; distinct from the stored student-line approval status.</summary>
public enum ChargeWorkflowStatus
{
	Pending = 0,
	InProcess = 1,
	Completed = 2,
	Rejected = 3
}
