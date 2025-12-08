using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// DTO for updating a leave policy
/// </summary>
public class UpdateLeavePolicyDto
{
	/// <summary>
	/// Name of the policy
	/// </summary>
	[Required]
	[StringLength(LeavePolicyConsts.MaxPolicyNameLength, MinimumLength = 1)]
	public string PolicyName { get; set; } = string.Empty;
	
	/// <summary>
	/// Associated leave type
	/// </summary>
	[Required]
	[StringLength(LeavePolicyConsts.MaxLeaveTypeLength, MinimumLength = 1)]
	public string LeaveType { get; set; } = string.Empty;
	
	/// <summary>
	/// Maximum days allowed per year (1-365)
	/// </summary>
	[Required]
	[Range(LeavePolicyConsts.MinMaximumDaysPerYear, LeavePolicyConsts.MaxMaximumDaysPerYear)]
	public int MaximumDaysPerYear { get; set; }
	
	/// <summary>
	/// Paid or Unpaid
	/// </summary>
	[Required]
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
	
	/// <summary>
	/// Optional description
	/// </summary>
	[StringLength(LeavePolicyConsts.MaxDescriptionLength)]
	public string? Description { get; set; }
}
