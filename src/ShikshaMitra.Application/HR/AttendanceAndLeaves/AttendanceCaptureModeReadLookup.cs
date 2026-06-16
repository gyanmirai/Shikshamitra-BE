using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class AttendanceCaptureModeReadLookup
{
	internal static Dictionary<(Guid EmployeeId, DateOnly Date), AttendanceCaptureMode> BuildFallbackLookup(
		List<Guid> employeeIds,
		List<DateOnly> dates,
		AttendanceCaptureMode fallback) =>
		employeeIds
			.SelectMany(employeeId => dates.Select(date => (employeeId, date)))
			.ToDictionary(x => (x.employeeId, x.date), _ => fallback);

	internal static Dictionary<(Guid EmployeeId, Guid AcademicYearId), HashSet<string>> BuildGroupKeysByEmployeeYear(
		List<Employee> employees,
		Dictionary<Guid, List<Group>> groupsByYear)
	{
		var lookup = new Dictionary<(Guid EmployeeId, Guid AcademicYearId), HashSet<string>>();
		foreach (var (academicYearId, groups) in groupsByYear)
		{
			foreach (var employee in employees)
			{
				var keys = groups
					.Where(group => HrPolicyGroupMembership.MatchesEmployee(group, employee))
					.Select(group => ToTargetKey(group.Id))
					.ToHashSet(StringComparer.OrdinalIgnoreCase);
				if (keys.Count > 0)
				{
					lookup[(employee.Id, academicYearId)] = keys;
				}
			}
		}

		return lookup;
	}

	internal static List<string> BuildTargetKeys(
		List<Guid> employeeIds,
		Dictionary<(Guid EmployeeId, Guid AcademicYearId), HashSet<string>> groupKeysByEmployeeYear) =>
		employeeIds
			.Select(ToTargetKey)
			.Concat(groupKeysByEmployeeYear.Values.SelectMany(x => x))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

	internal static HrPolicyAssignment? SelectAssignment(
		Guid employeeId,
		Guid academicYearId,
		DateOnly date,
		List<HrPolicyAssignment> assignments,
		Dictionary<(Guid EmployeeId, Guid AcademicYearId), HashSet<string>> groupKeysByEmployeeYear)
	{
		var employeeKey = ToTargetKey(employeeId);
		groupKeysByEmployeeYear.TryGetValue((employeeId, academicYearId), out var groupKeys);
		return assignments
			.Where(x => x.AcademicYearId == academicYearId && x.IsEffectiveOn(date.ToDateTime(TimeOnly.MinValue)))
			.Where(x => x.TargetType == HrPolicyAssignmentTargetType.Employee
				? string.Equals(x.TargetKey, employeeKey, StringComparison.OrdinalIgnoreCase)
				: groupKeys?.Contains(x.TargetKey) == true)
			.OrderByDescending(GetSpecificity)
			.ThenByDescending(x => x.Priority)
			.ThenByDescending(x => x.EffectiveFrom)
			.ThenBy(x => x.Id)
			.FirstOrDefault();
	}

	internal static AttendanceCaptureMode ResolveCaptureMode(
		HrPolicyAssignment? assignment,
		Guid academicYearId,
		AttendanceCaptureModePolicyLookup policyLookup,
		AttendanceCaptureMode fallback)
	{
		if (assignment is not null)
		{
			return policyLookup.AssignedModes.TryGetValue(assignment.AttendancePolicyId, out var policy) &&
			       policy.AcademicYearId == academicYearId
				? policy.CaptureMode
				: fallback;
		}

		return policyLookup.DefaultModes.GetValueOrDefault(academicYearId, fallback);
	}

	private static int GetSpecificity(HrPolicyAssignment assignment) =>
		assignment.TargetType == HrPolicyAssignmentTargetType.Employee ? 100 : 50;

	private static string ToTargetKey(Guid id) => id.ToString("D").ToLowerInvariant();
}

internal sealed record AttendanceCaptureModePolicyLookup(
	Dictionary<Guid, AttendanceCaptureMode> DefaultModes,
	Dictionary<Guid, (Guid AcademicYearId, AttendanceCaptureMode CaptureMode)> AssignedModes);
