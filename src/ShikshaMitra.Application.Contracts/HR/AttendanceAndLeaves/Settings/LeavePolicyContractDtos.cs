using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// Response for paginated list of leave policies (matches OpenAPI LeavePolicyListResponse)
/// </summary>
public class LeavePolicyListResponse
{
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<LeavePolicyItem> Items { get; set; } = new();
}

/// <summary>
/// Leave policy item in list response (matches OpenAPI LeavePolicyItem)
/// Note: id is int per contract but we use Guid internally, contract shows sequential id
/// </summary>
public class LeavePolicyItem
{
	/// <summary>
	/// Sequential row number for display (contract expects int)
	/// </summary>
	public int Id { get; set; }
	
	/// <summary>
	/// Internal unique identifier
	/// </summary>
	public Guid PolicyId { get; set; }
	
	/// <summary>
	/// Policy title (maps from PolicyName)
	/// </summary>
	public string Title { get; set; } = string.Empty;
	
	/// <summary>
	/// Leave type name
	/// </summary>
	public string LeaveType { get; set; } = string.Empty;
	
	/// <summary>
	/// Maximum days per year
	/// </summary>
	public int MaximumPerYear { get; set; }
	
	/// <summary>
	/// "Yes" or "No"
	/// </summary>
	public string CarryForward { get; set; } = "No";
	
	/// <summary>
	/// "Yes" or "No"
	/// </summary>
	public string Encashable { get; set; } = "No";
	
	/// <summary>
	/// "Paid" or "Unpaid"
	/// </summary>
	public string PaidType { get; set; } = "Paid";
	
	/// <summary>
	/// "Yes" or "No"
	/// </summary>
	public string RequiresApproval { get; set; } = "Yes";
}

/// <summary>
/// Request to create a leave policy (matches OpenAPI LeavePolicyCreateRequest)
/// </summary>
public class LeavePolicyCreateRequest
{
	/// <summary>
	/// Organization identifier (from query parameter in controller)
	/// </summary>
	public Guid MediumStageId { get; set; }
	
	/// <summary>
	/// Policy title
	/// </summary>
	[Required]
	[MinLength(1)]
	[StringLength(LeavePolicyConsts.MaxPolicyNameLength)]
	public string PolicyTitle { get; set; } = string.Empty;
	
	/// <summary>
	/// Leave type ID (UUID reference)
	/// </summary>
	[Required]
	public Guid LeaveType { get; set; }
	
	/// <summary>
	/// Maximum days per year
	/// </summary>
	[Required]
	[Range(0, 365)]
	public int MaxLeavePerYear { get; set; }
	
	/// <summary>
	/// "Paid" or "Unpaid"
	/// </summary>
	[Required]
	[RegularExpression("^(Paid|Unpaid)$", ErrorMessage = "PaidType must be 'Paid' or 'Unpaid'")]
	public string PaidType { get; set; } = "Paid";
	
	/// <summary>
	/// Whether unused leave can be carried forward
	/// </summary>
	[Required]
	public bool CarryForward { get; set; }
	
	/// <summary>
	/// Whether leave can be encashed
	/// </summary>
	[Required]
	public bool EncashmentAllowed { get; set; }
	
	/// <summary>
	/// Whether leave requires approval
	/// </summary>
	[Required]
	public bool RequiresApproval { get; set; }
	
	/// <summary>
	/// Optional description
	/// </summary>
	[StringLength(LeavePolicyConsts.MaxDescriptionLength)]
	public string? Description { get; set; }
}

/// <summary>
/// Full leave policy response (matches OpenAPI LeavePolicy)
/// </summary>
public class LeavePolicyResponse
{
	public string Id { get; set; } = string.Empty;
	public string PolicyTitle { get; set; } = string.Empty;
	public Guid LeaveType { get; set; }
	public int MaxLeavePerYear { get; set; }
	public string PaidType { get; set; } = "Paid";
	public bool CarryForward { get; set; }
	public bool EncashmentAllowed { get; set; }
	public bool RequiresApproval { get; set; }
	public string? Description { get; set; }
	public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Response after update (matches OpenAPI LeavePolicyUpdated)
/// </summary>
public class LeavePolicyUpdatedResponse
{
	public string Id { get; set; } = string.Empty;
	public string PolicyTitle { get; set; } = string.Empty;
	public Guid LeaveType { get; set; }
	public int MaxLeavePerYear { get; set; }
	public string PaidType { get; set; } = "Paid";
	public bool CarryForward { get; set; }
	public bool EncashmentAllowed { get; set; }
	public bool RequiresApproval { get; set; }
	public string? Description { get; set; }
	public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Response for delete operation (matches OpenAPI LeavePolicyDeleteResponse)
/// </summary>
public class LeavePolicyDeleteResponse
{
	public string Id { get; set; } = string.Empty;
	public string Message { get; set; } = "Leave policy deleted successfully";
	public DateTime DeletedAt { get; set; }
}

