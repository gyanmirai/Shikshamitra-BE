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
	public string? Color { get; set; }
}

public class LeavePolicyRuleItem
{
	public Guid Id { get; set; }
	public LeaveTypeReference LeaveType { get; set; } = new();
	public string AccrualPolicyType { get; set; } = "Monthly";
	public int LeaveCount { get; set; }
	public bool CarryForward { get; set; }
	public int? CarryForwardLimit { get; set; }
	public bool EncashmentAllowed { get; set; }
	public string? EncashmentTiming { get; set; }
	public string PaidType { get; set; } = "Paid";
	public bool RequiresApproval { get; set; }
	public int MinNoticeDays { get; set; }
	public int MaxDaysPerRequest { get; set; }
	public bool AllowHalfDay { get; set; }
	public int? AttachmentRequiredAfterDays { get; set; }
	public bool AllowNegativeBalance { get; set; }
	public bool IsActive { get; set; }
}

public class LeavePolicyItem
{
	public Guid Id { get; set; }
	public string PolicyTitle { get; set; } = string.Empty;
	public string Status { get; set; } = "Draft";
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public int RuleCount { get; set; }
	public string? Description { get; set; }
	public List<LeavePolicyRuleItem> Rules { get; set; } = new();
}

public class LeavePolicyRuleRequest
{
	public Guid LeaveTypeId { get; set; }
	public string AccrualPolicyType { get; set; } = "Monthly";
	public int LeaveCount { get; set; }
	public string PaidType { get; set; } = "Paid";
	public bool CarryForward { get; set; }
	public int? CarryForwardLimit { get; set; }
	public bool EncashmentAllowed { get; set; }
	public string? EncashmentTiming { get; set; }
	public bool RequiresApproval { get; set; } = true;
	public int MinNoticeDays { get; set; } = 2;
	public int MaxDaysPerRequest { get; set; } = 30;
	public bool AllowHalfDay { get; set; } = true;
	public int? AttachmentRequiredAfterDays { get; set; }
	public bool AllowNegativeBalance { get; set; }
	public bool IsActive { get; set; } = true;
}

public class LeavePolicyCreateRequest
{
	public string PolicyTitle { get; set; } = string.Empty;
	public string Status { get; set; } = "Active";
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public string? Description { get; set; }
	public List<LeavePolicyRuleRequest> Rules { get; set; } = new();
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

		RuleFor(x => x.Status)
			.NotEmpty()
			.Matches("^(Draft|Active|Archived)$")
			.WithMessage("Status must be Draft, Active, or Archived");

		RuleFor(x => x.EffectiveFrom)
			.NotEmpty();

		RuleFor(x => x.EffectiveTo)
			.Must((request, effectiveTo) => !effectiveTo.HasValue || effectiveTo.Value.Date >= request.EffectiveFrom.Date)
			.WithMessage("EffectiveTo cannot be before EffectiveFrom");

		RuleFor(x => x.Rules)
			.NotEmpty();

		RuleForEach(x => x.Rules)
			.SetValidator(new LeavePolicyRuleRequestValidator());

		RuleFor(x => x.Description)
			.MaximumLength(LeavePolicyConsts.MaxDescriptionLength);
	}
}

public class LeavePolicyRuleRequestValidator : AbstractValidator<LeavePolicyRuleRequest>
{
	public LeavePolicyRuleRequestValidator()
	{
		RuleFor(x => x.LeaveTypeId).NotEmpty();
		RuleFor(x => x.AccrualPolicyType).NotEmpty().Matches("^(Monthly|Yearly)$");
		RuleFor(x => x.LeaveCount).GreaterThanOrEqualTo(0);
		RuleFor(x => x.PaidType).NotEmpty().Matches("^(Paid|Unpaid)$");
		RuleFor(x => x.EncashmentTiming).Matches("^(next_payroll|end_of_year)$").When(x => x.EncashmentTiming is not null);
		RuleFor(x => x.EncashmentTiming).NotEmpty().When(x => x.EncashmentAllowed);
		RuleFor(x => x.MinNoticeDays).GreaterThanOrEqualTo(0);
		RuleFor(x => x.MaxDaysPerRequest).GreaterThan(0);
		RuleFor(x => x.AttachmentRequiredAfterDays).GreaterThanOrEqualTo(0).When(x => x.AttachmentRequiredAfterDays.HasValue);
	}
}

public class LeavePolicyUpdateRequestValidator : AbstractValidator<LeavePolicyUpdateRequest>
{
	public LeavePolicyUpdateRequestValidator()
	{
		Include(new LeavePolicyCreateRequestValidator());
	}
}

public class LeavePolicyResponse : LeavePolicyItem
{
	public DateTime CreatedAt { get; set; }
}

public class LeavePolicyDetailResponse : LeavePolicyItem
{
}

public class LeavePolicyDeleteResponse
{
	public string Id { get; set; } = string.Empty;
	public string Message { get; set; } = "Leave policy deleted successfully";
	public DateTime DeletedAt { get; set; }
}
