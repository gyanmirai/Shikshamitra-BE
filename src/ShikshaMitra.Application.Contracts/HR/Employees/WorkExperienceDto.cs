using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Employees;

public class WorkExperienceDto : EntityDto<Guid>
{
	// Foreign Key
	public Guid EmployeeId { get; set; }
	public string? OrganizationName { get; set; }
	public string? OrganizationAddress { get; set; }
	public DateOnly? FromDate { get; set; }
	public DateOnly? ToDate { get; set; }
	public string? Department { get; set; }
	public string? Designation { get; set; }
	public string? Responsibilities { get; set; }
	public string? References { get; set; }
}