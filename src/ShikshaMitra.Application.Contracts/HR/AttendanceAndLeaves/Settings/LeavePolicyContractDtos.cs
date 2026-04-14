using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeavePolicyListResponse
{
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<LeavePolicyItem> Items { get; set; } = new();
}

public class LeaveTypeReference
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
}

public class LeavePolicyItem
{
	public Guid Id { get; set; }
	public string PolicyTitle { get; set; } = string.Empty;
	public LeaveTypeReference LeaveType { get; set; } = new();
	public string AccrualPolicyType { get; set; } = "Monthly";
	public int LeaveCount { get; set; }
	public bool CarryForward { get; set; }
	public bool EncashmentAllowed { get; set; }
	public string? EncashmentTiming { get; set; }
	public string PaidType { get; set; } = "Paid";
	public bool RequiresApproval { get; set; }
	public string? Description { get; set; }
}

public class LeavePolicyCreateRequest
{
	[Required]
	[MinLength(1)]
	[StringLength(LeavePolicyConsts.MaxPolicyNameLength)]
	public string PolicyTitle { get; set; } = string.Empty;

	[Required]
	public Guid LeaveType { get; set; }

	[Required]
	[RegularExpression("^(Monthly|Yearly)$", ErrorMessage = "AccrualPolicyType must be Monthly or Yearly")]
	public string AccrualPolicyType { get; set; } = "Monthly";

	[Required]
	[Range(LeavePolicyConsts.MinLeaveCount, LeavePolicyConsts.MaxLeaveCount)]
	public int LeaveCount { get; set; }

	[Required]
	[RegularExpression("^(Paid|Unpaid)$", ErrorMessage = "PaidType must be 'Paid' or 'Unpaid'")]
	public string PaidType { get; set; } = "Paid";

	[Required]
	public bool CarryForward { get; set; }

	[Required]
	public bool EncashmentAllowed { get; set; }

	[RegularExpression("^(next_payroll|end_of_year)$", ErrorMessage = "EncashmentTiming must be next_payroll or end_of_year")]
	public string? EncashmentTiming { get; set; }

	[Required]
	public bool RequiresApproval { get; set; }

	[StringLength(LeavePolicyConsts.MaxDescriptionLength)]
	public string? Description { get; set; }
}

public class LeavePolicyUpdateRequest : LeavePolicyCreateRequest
{
}

public class LeavePolicyResponse
{
	public Guid Id { get; set; }
	public string PolicyTitle { get; set; } = string.Empty;
	public Guid LeaveType { get; set; }
	public string AccrualPolicyType { get; set; } = "Monthly";
	public int LeaveCount { get; set; }
	public string PaidType { get; set; } = "Paid";
	public bool CarryForward { get; set; }
	public bool EncashmentAllowed { get; set; }
	public string? EncashmentTiming { get; set; }
	public bool RequiresApproval { get; set; }
	public string? Description { get; set; }
	public DateTime CreatedAt { get; set; }
}

public class LeavePolicyDetailResponse
{
	public Guid Id { get; set; }
	public string PolicyTitle { get; set; } = string.Empty;
	public LeaveTypeReference LeaveType { get; set; } = new();
	public string AccrualPolicyType { get; set; } = "Monthly";
	public int LeaveCount { get; set; }
	public string PaidType { get; set; } = "Paid";
	public bool CarryForward { get; set; }
	public bool EncashmentAllowed { get; set; }
	public string? EncashmentTiming { get; set; }
	public bool RequiresApproval { get; set; }
	public string? Description { get; set; }
}

public class LeavePolicyDeleteResponse
{
	public string Id { get; set; } = string.Empty;
	public string Message { get; set; } = "Leave policy deleted successfully";
	public DateTime DeletedAt { get; set; }
}
