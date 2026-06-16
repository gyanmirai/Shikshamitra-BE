using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Policies;

internal sealed record WorkCalendarPolicyReferences(
	HashSet<Guid> PayrollPolicyIds,
	HashSet<Guid> AttendancePolicyIds,
	List<string> PayrollPolicyNames,
	List<string> AttendancePolicyNames)
{
	public static WorkCalendarPolicyReferences Empty { get; } = new([], [], [], []);
	public bool IsEmpty => PayrollPolicyIds.Count == 0 && AttendancePolicyIds.Count == 0;
	public int PayrollPolicyCount => PayrollPolicyIds.Count;
	public int AttendancePolicyCount => AttendancePolicyIds.Count;
}

internal sealed record AffectedEmployeePreview(int Count, List<Employee> Sample, List<Guid> EmployeeIds)
{
	public static AffectedEmployeePreview Empty { get; } = new(0, [], []);
}
