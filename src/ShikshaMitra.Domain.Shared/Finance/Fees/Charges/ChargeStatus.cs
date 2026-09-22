namespace ShikshaMitra.Finance.Fees.Charges;

/// <summary>
/// Represents the status of a charge
/// </summary>
public enum ChargeStatus
{
	/// <summary>
	/// Charge is pending approval
	/// </summary>
	Pending = 0,

	/// <summary>
	/// Charge has been approved
	/// </summary>
	Approved = 1,

	/// <summary>
	/// Charge has been rejected
	/// </summary>
	Rejected = 2,

	/// <summary>
	/// An approved, unpaid charge was voided with an auditable reason.
	/// </summary>
	Voided = 3
}
