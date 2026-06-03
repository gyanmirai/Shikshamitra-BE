using FluentValidation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeaveTypeInfo;

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
		RuleFor(x => x.Name).NotEmpty();
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
		RuleFor(x => x.Name).NotEmpty().When(x => x.Name != null);
		RuleFor(x => x.Color)
			.Matches(@"^#([A-Fa-f0-9]{6})$")
			.WithMessage("Color must be in HEX format (#RRGGBB)")
			.When(x => x.Color != null);
	}
}

public class LeaveTypeResponse
{
	[Required]
	public Guid Id { get; set; }

	[Required]
	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }

	[Required]
	public string Color { get; set; } = string.Empty;
}

public class LeaveTypeItemResponse
{
	[Required]
	public Guid Id { get; set; }

	[Required]
	[MinLength(1)]
	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }

	[Required]
	[RegularExpression(@"^#([A-Fa-f0-9]{6})$")]
	public string Color { get; set; } = string.Empty;
}

public class LeaveTypeListResponse
{
	[Required]
	public int TotalCount { get; set; }

	[Required]
	public int CurrentPage { get; set; }

	[Required]
	public int PageSize { get; set; }

	[Required]
	public int TotalPages { get; set; }

	[Required]
	public List<LeaveTypeItemResponse> Items { get; set; } = new();
}

public class LeaveTypeDeleteResponse
{
	[Required]
	public Guid Id { get; set; }

	[Required]
	public string Message { get; set; } = "Leave type deleted successfully";
}
