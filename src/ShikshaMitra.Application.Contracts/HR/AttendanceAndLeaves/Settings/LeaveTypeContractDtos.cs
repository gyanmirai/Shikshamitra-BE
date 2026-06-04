using System;
using System.Collections.Generic;
using FluentValidation;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeaveTypeListResponse
{
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<LeaveTypeItem> Items { get; set; } = new();
}

public class LeaveTypeItem
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string Color { get; set; } = string.Empty;
}

public class LeaveTypeResponse
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string Color { get; set; } = string.Empty;
}

public class LeaveTypeCreateRequest
{
	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }

	public string Color { get; set; } = string.Empty;
}

public class LeaveTypeUpdateRequest
{
	public string? Name { get; set; }

	public string? Description { get; set; }

	public string? Color { get; set; }
}

public class LeaveTypeCreateRequestValidator : AbstractValidator<LeaveTypeCreateRequest>
{
	public LeaveTypeCreateRequestValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.MaximumLength(LeaveTypeConsts.MaxTypeLength);

		RuleFor(x => x.Description)
			.MaximumLength(LeaveTypeConsts.MaxDescriptionLength);

		RuleFor(x => x.Color)
			.NotEmpty()
			.Matches(@"^#([A-Fa-f0-9]{6})$")
			.WithMessage("Color must be in HEX format (#RRGGBB)");
	}
}

public class LeaveTypeUpdateRequestValidator : AbstractValidator<LeaveTypeUpdateRequest>
{
	public LeaveTypeUpdateRequestValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.MaximumLength(LeaveTypeConsts.MaxTypeLength)
			.When(x => x.Name is not null);

		RuleFor(x => x.Description)
			.MaximumLength(LeaveTypeConsts.MaxDescriptionLength);

		RuleFor(x => x.Color)
			.Matches(@"^#([A-Fa-f0-9]{6})$")
			.WithMessage("Color must be in HEX format (#RRGGBB)")
			.When(x => x.Color is not null);
	}
}

public class LeaveTypeDeleteResponse
{
	public Guid Id { get; set; }
	public string Message { get; set; } = "Leave type deleted successfully";
}
