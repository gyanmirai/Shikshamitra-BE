using System;
using System.Collections.Generic;
using ShikshaMitra.Common;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Response DTO for employee status change operation.
/// </summary>
public class EmployeeStatusResponseDto
{
	/// <summary>
	/// The employee's unique identifier.
	/// </summary>
	public string EmployeeId { get; set; } = string.Empty;

	/// <summary>
	/// The previous status before the change.
	/// </summary>
	public EmployeeStatusType? PreviousStatus { get; set; }

	/// <summary>
	/// The current status after the change.
	/// </summary>
	public EmployeeStatusType CurrentStatus { get; set; }

	/// <summary>
	/// Timestamp of when the status was updated.
	/// </summary>
	public DateTimeOffset UpdatedAt { get; set; }

	/// <summary>
	/// Effective lifecycle/payroll date for non-active statuses.
	/// </summary>
	public DateOnly? EffectiveDate { get; set; }
}

/// <summary>
/// Response DTO for employee deactivation eligibility checks.
/// </summary>
public class EmployeeStatusDeactivationCheckDto
{
	public bool CanDeactivate { get; set; }
	public int ActiveDirectReportCount { get; set; }
}

public class EmployeeActivationReadinessDto
{
	public bool CanActivate { get; set; }
	public DateTime RequiredMonth { get; set; }
	public Guid? CompensationRevisionId { get; set; }
	public List<string> BlockerCodes { get; set; } = [];
}

