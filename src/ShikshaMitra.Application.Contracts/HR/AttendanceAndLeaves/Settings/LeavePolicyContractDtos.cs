using System;
using System.Collections.Generic;
using FluentValidation;

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
}

public class LeavePolicyUpdateRequest : LeavePolicyCreateRequest
{
}

public class LeavePolicyCreateRequestValidator : AbstractValidator<LeavePolicyCreateRequest>
{
	public LeavePolicyCreateRequestValidator()
	{
		RuleFor(x => x.PolicyTitle)
			.NotEmpty()
			.MaximumLength(LeavePolicyConsts.MaxPolicyNameLength);

		RuleFor(x => x.LeaveType)
			.NotEmpty();

		RuleFor(x => x.AccrualPolicyType)
			.NotEmpty()
			.Matches("^(Monthly|Yearly)$")
			.WithMessage("AccrualPolicyType must be Monthly or Yearly");

		RuleFor(x => x.LeaveCount)
			.InclusiveBetween(LeavePolicyConsts.MinLeaveCount, LeavePolicyConsts.MaxLeaveCount);

		RuleFor(x => x.PaidType)
			.NotEmpty()
			.Matches("^(Paid|Unpaid)$")
			.WithMessage("PaidType must be 'Paid' or 'Unpaid'");

		RuleFor(x => x.EncashmentTiming)
			.Matches("^(next_payroll|end_of_year)$")
			.WithMessage("EncashmentTiming must be next_payroll or end_of_year")
			.When(x => x.EncashmentTiming is not null);

		RuleFor(x => x.Description)
			.MaximumLength(LeavePolicyConsts.MaxDescriptionLength);
	}
}

public class LeavePolicyUpdateRequestValidator : AbstractValidator<LeavePolicyUpdateRequest>
{
	public LeavePolicyUpdateRequestValidator()
	{
		Include(new LeavePolicyCreateRequestValidator());
	}
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
