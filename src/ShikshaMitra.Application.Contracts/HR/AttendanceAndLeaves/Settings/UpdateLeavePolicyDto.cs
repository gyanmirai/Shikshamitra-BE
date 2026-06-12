namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// DTO for updating a leave policy
/// </summary>
public class UpdateLeavePolicyDto
{
	/// <summary>
	/// Name of the policy
	/// </summary>
	public string PolicyName { get; set; } = string.Empty;

	/// <summary>
	/// Associated leave type
	/// </summary>
	public string LeaveType { get; set; } = string.Empty;

	/// <summary>
	/// Maximum days allowed per year (1-365)
	/// </summary>
	public int MaximumDaysPerYear { get; set; }

	/// <summary>
	/// Paid or Unpaid
	/// </summary>
	public LeavePaidType PaidType { get; set; }

	/// <summary>
	/// Can carry forward unused leave to next year
	/// </summary>
	public bool CarryForward { get; set; } = false;

	/// <summary>
	/// Can encash unused leave
	/// </summary>
	public bool Encashable { get; set; } = false;

	/// <summary>
	/// Requires manager/admin approval
	/// </summary>
	public bool RequiresApproval { get; set; } = true;

	public bool IsDefault { get; set; } = false;

	/// <summary>
	/// Optional description
	/// </summary>
	public string? Description { get; set; }
}
