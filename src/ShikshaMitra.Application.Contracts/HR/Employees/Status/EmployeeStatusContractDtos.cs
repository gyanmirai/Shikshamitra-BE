using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Employees.Status;

/// <summary>
/// API contract: change employee employment status.
/// </summary>
public class EmployeeStatusUpdateRequest
{
	/// <summary>0=Active, 1=Inactive, 2=Left, 3=Retired</summary>
	public int EmployeeStatusType { get; set; }

	public string? Remarks { get; set; }

	/// <summary>Effective lifecycle/payroll date. Required for 1=Inactive, 2=Left, 3=Retired.</summary>
	public DateOnly? EffectiveDate { get; set; }
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

	public DateOnly? EffectiveDate { get; set; }
}

/// <summary>
/// API contract: deactivation eligibility result for an employee.
/// </summary>
public class EmployeeStatusDeactivationCheckResponse
{
	public bool CanDeactivate { get; set; }
	public int ActiveDirectReportCount { get; set; }
}

public class EmployeeActivationReadinessResponse
{
	public bool CanActivate { get; set; }
	public DateTime RequiredMonth { get; set; }
	public Guid? CompensationRevisionId { get; set; }
	public List<string> BlockerCodes { get; set; } = [];
}
