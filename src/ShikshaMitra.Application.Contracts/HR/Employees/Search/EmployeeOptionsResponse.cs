using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Employees.Search;

public class EmployeeOptionsResponse
{
	public List<EmployeeOptionDto> Items { get; set; } = [];
	public bool HasMore { get; set; }
}

public class EmployeeOptionDto
{
	public Guid Id { get; set; }
	public string FullName { get; set; } = string.Empty;
	public int Status { get; set; }
}
