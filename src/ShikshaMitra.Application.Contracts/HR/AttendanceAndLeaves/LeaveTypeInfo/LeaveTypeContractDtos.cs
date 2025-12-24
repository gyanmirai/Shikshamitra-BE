using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeaveTypeInfo;

/// <summary>
/// Request DTO for creating a leave type
/// </summary>
public class LeaveTypeCreateRequest
{
	[Required]
	[MinLength(1)]
	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }

	[Required]
	[RegularExpression(@"^#([A-Fa-f0-9]{6})$", ErrorMessage = "Color must be in HEX format (#RRGGBB)")]
	public string Color { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for updating a leave type
/// </summary>
public class LeaveTypeUpdateRequest
{
	[MinLength(1)]
	public string? Name { get; set; }

	public string? Description { get; set; }

	[RegularExpression(@"^#([A-Fa-f0-9]{6})$", ErrorMessage = "Color must be in HEX format (#RRGGBB)")]
	public string? Color { get; set; }
}

/// <summary>
/// Response DTO for leave type
/// </summary>
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

/// <summary>
/// Response DTO for leave type list item
/// </summary>
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

/// <summary>
/// Response DTO for paginated leave type list
/// </summary>
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

/// <summary>
/// Response DTO for delete operation
/// </summary>
public class LeaveTypeDeleteResponse
{
	[Required]
	public Guid Id { get; set; }

	[Required]
	public string Message { get; set; } = "Leave type deleted successfully";
}
