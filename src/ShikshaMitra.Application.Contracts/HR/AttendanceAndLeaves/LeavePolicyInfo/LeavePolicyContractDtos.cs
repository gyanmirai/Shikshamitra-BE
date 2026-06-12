using FluentValidation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeavePolicyInfo;

public class LeavePolicyCreateRequest
{
	public string PolicyTitle { get; set; } = string.Empty;

	public Guid LeaveType { get; set; }

	public int MaxLeavePerYear { get; set; }

	public string PaidType { get; set; } = string.Empty;

	public bool CarryForward { get; set; }

	public bool EncashmentAllowed { get; set; }

	public bool RequiresApproval { get; set; }

	public bool IsDefault { get; set; }

	public string? Description { get; set; }
}

public class LeavePolicyCreateRequestValidator : AbstractValidator<LeavePolicyCreateRequest>
{
	public LeavePolicyCreateRequestValidator()
	{
		RuleFor(x => x.PolicyTitle).NotEmpty();
		RuleFor(x => x.LeaveType).NotEmpty();
		RuleFor(x => x.MaxLeavePerYear).GreaterThanOrEqualTo(0);
		RuleFor(x => x.PaidType).NotEmpty();
	}
}

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

	public bool IsDefault { get; set; }

	public string? Description { get; set; }

	[Required]
	public DateTime CreatedAt { get; set; }
}

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

	public bool IsDefault { get; set; }

	public string? Description { get; set; }

	[Required]
	public DateTime UpdatedAt { get; set; }
}

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

public class LeavePolicyDeleteResponse
{
	[Required]
	public string Id { get; set; } = string.Empty;

	[Required]
	public string Message { get; set; } = "Leave policy deleted successfully";

	[Required]
	public DateTime DeletedAt { get; set; }
}
