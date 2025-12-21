using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees.ReportingManager;

public class AssignReportingManagerDto
{
	[Required]
	public Guid ReportingManagerId { get; set; }
}
