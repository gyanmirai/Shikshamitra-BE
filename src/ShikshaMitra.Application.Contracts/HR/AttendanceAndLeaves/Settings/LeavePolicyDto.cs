using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// DTO for leave policy
/// </summary>
public class LeavePolicyDto : FullAuditedEntityDto<Guid>
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
	/// Maximum days allowed per year
	/// </summary>
	public int MaximumDaysPerYear { get; set; }

	/// <summary>
	/// Paid or Unpaid leave type
	/// </summary>
	public string PaidType { get; set; } = string.Empty;

	/// <summary>
	/// Can carry forward unused leave
	/// </summary>
	public bool CarryForward { get; set; }

	/// <summary>
	/// Can encash unused leave
	/// </summary>
	public bool Encashable { get; set; }

	/// <summary>
	/// Requires approval
	/// </summary>
	public bool RequiresApproval { get; set; }

	/// <summary>
	/// Optional description
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// Whether this policy is active
	/// </summary>
	public bool IsActive { get; set; }
}
