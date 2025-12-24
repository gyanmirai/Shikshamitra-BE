using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeavePolicyInfo;

/// <summary>
/// Request DTO for creating a leave policy
/// </summary>
public class LeavePolicyCreateRequest
{
	[Required]
	[MinLength(1)]
	public string PolicyTitle { get; set; } = string.Empty;

	[Required]
	public Guid LeaveType { get; set; }

	[Required]
	[Range(0, int.MaxValue)]
	public int MaxLeavePerYear { get; set; }

	[Required]
	public string PaidType { get; set; } = string.Empty;

	[Required]
	public bool CarryForward { get; set; }

	[Required]
	public bool EncashmentAllowed { get; set; }

	[Required]
	public bool RequiresApproval { get; set; }

	public string? Description { get; set; }
}

/// <summary>
/// Response DTO for leave policy
/// </summary>
public class LeavePolicyResponse
{
	[Required]
	public string Id { get; set; } = string.Empty;

	[Required]
	public string PolicyTitle { get; set; } = string.Empty;

	[Required]
	public Guid LeaveType { get; set; }

	[Required]
	public int MaxLeavePerYear { get; set; }

	[Required]
	public string PaidType { get; set; } = string.Empty;

	[Required]
	public bool CarryForward { get; set; }

	[Required]
	public bool EncashmentAllowed { get; set; }

	[Required]
	public bool RequiresApproval { get; set; }

	public string? Description { get; set; }

	[Required]
	public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Response DTO for updated leave policy
/// </summary>
public class LeavePolicyUpdatedResponse
{
	[Required]
	public string Id { get; set; } = string.Empty;

	[Required]
	public string PolicyTitle { get; set; } = string.Empty;

	[Required]
	public Guid LeaveType { get; set; }

	[Required]
	public int MaxLeavePerYear { get; set; }

	[Required]
	public string PaidType { get; set; } = string.Empty;

	[Required]
	public bool CarryForward { get; set; }

	[Required]
	public bool EncashmentAllowed { get; set; }

	[Required]
	public bool RequiresApproval { get; set; }

	public string? Description { get; set; }

	[Required]
	public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Response DTO for leave policy list item
/// </summary>
public class LeavePolicyItemResponse
{
	[Required]
	public int Id { get; set; }

	[Required]
	public string Title { get; set; } = string.Empty;

	[Required]
	public string LeaveType { get; set; } = string.Empty;

	[Required]
	[Range(0, int.MaxValue)]
	public int MaximumPerYear { get; set; }

	[Required]
	public string CarryForward { get; set; } = string.Empty;

	[Required]
	public string Encashable { get; set; } = string.Empty;

	[Required]
	public string PaidType { get; set; } = string.Empty;

	[Required]
	public string RequiresApproval { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO for paginated leave policy list
/// </summary>
public class LeavePolicyListResponse
{
	[Required]
	public int TotalCount { get; set; }

	[Required]
	[Range(1, int.MaxValue)]
	public int CurrentPage { get; set; }

	[Required]
	[Range(10, 100)]
	public int PageSize { get; set; }

	[Required]
	[Range(1, int.MaxValue)]
	public int TotalPages { get; set; }

	[Required]
	public List<LeavePolicyItemResponse> Items { get; set; } = new();
}

/// <summary>
/// Response DTO for delete operation
/// </summary>
public class LeavePolicyDeleteResponse
{
	[Required]
	public string Id { get; set; } = string.Empty;

	[Required]
	public string Message { get; set; } = "Leave policy deleted successfully";

	[Required]
	public DateTime DeletedAt { get; set; }
}
