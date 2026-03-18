using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.HR.Employees;

public class WorkExperience : ValueObject
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

	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return OrganizationName ?? string.Empty;
		yield return OrganizationAddress ?? string.Empty;
		yield return FromDate?.ToString() ?? string.Empty;
		yield return ToDate?.ToString() ?? string.Empty;
		yield return Department ?? string.Empty;
		yield return Designation ?? string.Empty;
		yield return Responsibilities ?? string.Empty;
		yield return References ?? string.Empty;
	}
}