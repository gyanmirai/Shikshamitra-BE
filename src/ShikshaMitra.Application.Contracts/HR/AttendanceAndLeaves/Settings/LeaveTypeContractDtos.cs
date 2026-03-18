using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// Response for paginated list of leave types (matches OpenAPI LeaveTypeListResponse)
/// </summary>
public class LeaveTypeListResponse
{
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<LeaveTypeItem> Items { get; set; } = new();
}

/// <summary>
/// Leave type item in list response (matches OpenAPI LeaveTypeItem)
/// </summary>
public class LeaveTypeItem
{
	public Guid Id { get; set; }

	/// <summary>
	/// Name of the leave type (maps from Type property)
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Optional description
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// Color code in HEX format (#RRGGBB)
	/// </summary>
	public string Color { get; set; } = string.Empty;
}

/// <summary>
/// Full leave type response (matches OpenAPI LeaveType)
/// </summary>
public class LeaveTypeResponse
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string Color { get; set; } = string.Empty;
}

/// <summary>
/// Request to create a leave type (matches OpenAPI LeaveTypeCreateRequest)
/// </summary>
public class LeaveTypeCreateRequest
{
	/// <summary>
	/// Organization identifier (from query parameter in controller)
	/// </summary>
	public Guid MediumStageId { get; set; }

	/// <summary>
	/// Name of the leave type
	/// </summary>
	[Required]
	[MinLength(1)]
	[StringLength(LeaveTypeConsts.MaxTypeLength)]
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Optional description
	/// </summary>
	[StringLength(LeaveTypeConsts.MaxDescriptionLength)]
	public string? Description { get; set; }

	/// <summary>
	/// Color code in HEX format (#RRGGBB)
	/// </summary>
	[Required]
	[RegularExpression(@"^#([A-Fa-f0-9]{6})$", ErrorMessage = "Color must be in HEX format (#RRGGBB)")]
	public string Color { get; set; } = string.Empty;
}

/// <summary>
/// Request to update a leave type (matches OpenAPI LeaveTypeUpdateRequest)
/// </summary>
public class LeaveTypeUpdateRequest
{
	/// <summary>
	/// Name of the leave type
	/// </summary>
	[MinLength(1)]
	[StringLength(LeaveTypeConsts.MaxTypeLength)]
	public string? Name { get; set; }

	/// <summary>
	/// Optional description
	/// </summary>
	[StringLength(LeaveTypeConsts.MaxDescriptionLength)]
	public string? Description { get; set; }

	/// <summary>
	/// Color code in HEX format (#RRGGBB)
	/// </summary>
	[RegularExpression(@"^#([A-Fa-f0-9]{6})$", ErrorMessage = "Color must be in HEX format (#RRGGBB)")]
	public string? Color { get; set; }
}

/// <summary>
/// Response for delete operation (matches OpenAPI LeaveTypeDeleteResponse)
/// </summary>
public class LeaveTypeDeleteResponse
{
	public Guid Id { get; set; }
	public string Message { get; set; } = "Leave type deleted successfully";
}

