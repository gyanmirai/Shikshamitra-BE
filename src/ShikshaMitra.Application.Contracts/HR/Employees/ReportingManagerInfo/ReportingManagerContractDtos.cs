using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

using FluentValidation;

namespace ShikshaMitra.HR.Employees.ReportingManagerInfo;

/// <summary>
/// Request DTO for assigning/updating reporting manager
/// </summary>
public class ReportingManagerRequest
{
	public string? ReportingManagerId { get; set; }
}

public class ReportingManagerRequestValidator : FluentValidation.AbstractValidator<ReportingManagerRequest>
{
	public ReportingManagerRequestValidator()
	{
		RuleFor(x => x.ReportingManagerId)
			.NotEmpty()
			.Must(value => Guid.TryParse(value, out var id) && id != Guid.Empty)
			.WithMessage("Reporting manager is required.");
	}
}

/// <summary>
/// Response DTO for reporting manager information
/// </summary>
public class ReportingManagerResponse
{
	[Required]
	public Guid EmployeeId { get; set; }

	public ManagerInfo? ReportingManager { get; set; }
}

/// <summary>
/// Response DTO for employees under a manager
/// </summary>
public class EmployeesUnderManagerResponse
{
	[Required]
	public ManagerInfo Manager { get; set; } = null!;

	[Required]
	public List<ManagerEmployeeInfo> Employees { get; set; } = new();
}

/// <summary>
/// Manager information
/// </summary>
public class ManagerInfo
{
	[Required]
	public Guid Id { get; set; }

	[Required]
	public string EmployeeIdentifier { get; set; } = string.Empty;

	[Required]
	public string FullName { get; set; } = string.Empty;

	[Required]
	public string Designation { get; set; } = string.Empty;
}

/// <summary>
/// Employee information under manager
/// </summary>
public class ManagerEmployeeInfo
{
	[Required]
	public Guid Id { get; set; }

	[Required]
	public string EmployeeIdentifier { get; set; } = string.Empty;

	[Required]
	public string FullName { get; set; } = string.Empty;

	[Required]
	public string Designation { get; set; } = string.Empty;
}
