using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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
	[Required]
	[MinLength(1)]
	[StringLength(LeaveTypeConsts.MaxTypeLength)]
	public string Name { get; set; } = string.Empty;

	[StringLength(LeaveTypeConsts.MaxDescriptionLength)]
	public string? Description { get; set; }

	[Required]
	[RegularExpression(@"^#([A-Fa-f0-9]{6})$", ErrorMessage = "Color must be in HEX format (#RRGGBB)")]
	public string Color { get; set; } = string.Empty;
}

public class LeaveTypeUpdateRequest
{
	[MinLength(1)]
	[StringLength(LeaveTypeConsts.MaxTypeLength)]
	public string? Name { get; set; }

	[StringLength(LeaveTypeConsts.MaxDescriptionLength)]
	public string? Description { get; set; }

	[RegularExpression(@"^#([A-Fa-f0-9]{6})$", ErrorMessage = "Color must be in HEX format (#RRGGBB)")]
	public string? Color { get; set; }
}

public class LeaveTypeDeleteResponse
{
	public Guid Id { get; set; }
	public string Message { get; set; } = "Leave type deleted successfully";
}
