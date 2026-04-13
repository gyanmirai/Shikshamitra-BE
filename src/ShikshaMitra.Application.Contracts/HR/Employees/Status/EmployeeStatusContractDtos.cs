using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees.Status;

/// <summary>
/// API contract: change employee employment status.
/// </summary>
public class EmployeeStatusUpdateRequest
{
	/// <summary>0=Active, 1=Inactive, 2=Left, 3=Retired</summary>
	[Required]
	[Range(0, 3)]
	public int EmployeeStatusType { get; set; }

	[MaxLength(2000)]
	public string? Remarks { get; set; }
}

/// <summary>
/// API contract: result of a status change (or idempotent no-op).
/// </summary>
public class EmployeeStatusUpdateResponse
{
	public string EmployeeId { get; set; } = string.Empty;

	/// <summary>Prior status; null if previously unset.</summary>
	public int? PreviousStatus { get; set; }

	public int CurrentStatus { get; set; }

	public DateTimeOffset UpdatedAt { get; set; }
}
