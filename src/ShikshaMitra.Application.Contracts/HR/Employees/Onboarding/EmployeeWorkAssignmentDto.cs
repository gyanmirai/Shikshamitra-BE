using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Employees.Onboarding;

// Organizational context only. Never an input to access-scope assignment.
public class EmployeeWorkAssignmentDto
{
	public string Mode { get; set; } = EmployeeWorkAssignmentModes.DecideLater;
	public Guid? AcademicYearId { get; set; }
	public List<Guid> OrganizationIds { get; set; } = [];
}

public class EmployeeWorkAssignmentSummaryDto : EmployeeWorkAssignmentDto
{
	public string? AcademicYearName { get; set; }
	public List<string> OrganizationLabels { get; set; } = [];
}
