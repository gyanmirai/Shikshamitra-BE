using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees;

public class WorkExperience
{
	public string? OrganizationName { get; private set; }
	public string? OrganizationAddress { get; private set; }
	public DateOnly? FromDate { get; private set; }
	public DateOnly? ToDate { get; private set; }
	public string? Department { get; private set; }
	public string? Designation { get; private set; }
	public string? Responsibilities { get; private set; }
	public string? References { get; private set; }

	private WorkExperience() { } // For ORM

	public WorkExperience(
		string? organizationName,
		string? organizationAddress,
		DateOnly? fromDate,
		DateOnly? toDate,
		string? department,
		string? designation,
		string? responsibilities,
		string? references)
	{
		OrganizationName = organizationName;
		OrganizationAddress = organizationAddress;
		FromDate = fromDate;
		ToDate = toDate;
		Department = department;
		Designation = designation;
		Responsibilities = responsibilities;
		References = references;
	}
}