using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees;

namespace ShikshaMitra.HR.Policies;

internal static class HrPolicyAssignmentProjection
{
	public static HrPolicyEffectiveAssignmentCandidateDto MapCandidate(
		Guid? assignmentId,
		string targetKey,
		string? targetDisplayName,
		Guid payrollPolicyId,
		Guid attendancePolicyId,
		DateTime effectiveFrom,
		DateTime? effectiveTo,
		int priority,
		IReadOnlyDictionary<Guid, string> payrollNames,
		IReadOnlyDictionary<Guid, string> attendanceNames) => new()
	{
		AssignmentId = assignmentId,
		TargetType = "group",
		TargetKey = targetKey,
		TargetDisplayName = targetDisplayName,
		PayrollPolicyId = payrollPolicyId,
		PayrollPolicyName = payrollNames.GetValueOrDefault(payrollPolicyId) ?? "Unknown payroll policy",
		AttendancePolicyId = attendancePolicyId,
		AttendancePolicyName = attendanceNames.GetValueOrDefault(attendancePolicyId) ?? "Unknown attendance policy",
		EffectiveFrom = effectiveFrom.Date,
		EffectiveTo = effectiveTo?.Date,
		Priority = priority
	};

	public static HrPolicyAffectedEmployeeDto MapAffectedEmployee(Employee employee) => new()
	{
		EmployeeId = employee.Id,
		EmployeeCode = employee.EmployeeCode,
		EmployeeName = employee.EmployeeName,
		Department = employee.Department,
		RoleId = employee.RoleId,
		ShiftId = employee.ShiftId,
		EmployeeType = employee.EmployeeGroup?.ToString()
	};

	public static EffectiveEmployeePoliciesDto ToEffectivePoliciesDto(HrResolvedPolicies resolved)
	{
		return new EffectiveEmployeePoliciesDto
		{
			EmployeeId = resolved.EmployeeId,
			AcademicYearId = resolved.AcademicYearId,
			Date = resolved.Date,
			Assignment = resolved.Assignment == null ? null : HrPolicyMapping.ToDto(resolved.Assignment),
			PolicySource = resolved.Assignment == null ? "default" : HrPolicyMapping.MapAssignmentTargetType(resolved.Assignment.TargetType),
			MatchedAssignmentId = resolved.Assignment?.Id,
			MatchedTargetType = resolved.Assignment == null ? null : HrPolicyMapping.MapAssignmentTargetType(resolved.Assignment.TargetType),
			MatchedTargetKey = resolved.Assignment?.TargetKey,
			MatchedTargetDisplayName = resolved.Assignment?.TargetDisplayName,
			MatchedPriority = resolved.Assignment?.Priority,
			AttendancePolicy = HrPolicyMapping.ToDto(resolved.AttendancePolicy),
			PayrollPolicy = HrPolicyMapping.ToDto(resolved.PayrollPolicy),
			AttendanceWorkCalendarPolicy = HrPolicyMapping.ToDto(resolved.AttendanceWorkCalendarPolicy),
			PayrollWorkCalendarPolicy = HrPolicyMapping.ToDto(resolved.PayrollWorkCalendarPolicy),
			ResolutionSteps = resolved.ResolutionSteps.Select(x => new EffectivePolicyResolutionStepDto
			{
				Source = x.Source,
				TargetType = x.TargetType.HasValue ? HrPolicyMapping.MapAssignmentTargetType(x.TargetType.Value) : "default",
				TargetKey = x.TargetKey,
				TargetDisplayName = x.TargetDisplayName,
				AssignmentId = x.AssignmentId,
				Priority = x.Priority,
				Status = x.Status,
				SkipReason = x.SkipReason,
				PayrollPolicyId = x.PayrollPolicyId,
				AttendancePolicyId = x.AttendancePolicyId
			}).ToList()
		};
	}

	public static string BuildAssignmentAuditName(HrPolicyAssignment assignment) =>
		$"{HrPolicyMapping.MapAssignmentTargetType(assignment.TargetType)}: {assignment.TargetDisplayName ?? assignment.TargetKey}";

	public static void CollectTargetId(string? targetType, string? targetKey, HashSet<Guid> employeeIds, HashSet<Guid> groupIds)
	{
		if (!Guid.TryParse(targetKey, out var id))
		{
			return;
		}

		if (string.Equals(targetType, "employee", StringComparison.OrdinalIgnoreCase))
		{
			employeeIds.Add(id);
		}
		else if (string.Equals(targetType, "group", StringComparison.OrdinalIgnoreCase))
		{
			groupIds.Add(id);
		}
	}

	public static string? ResolveTargetDisplayName(
		string? targetType,
		string? targetKey,
		string? currentDisplayName,
		IReadOnlyDictionary<string, string> employeeNames,
		IReadOnlyDictionary<string, string> groupNames)
	{
		if (!string.IsNullOrWhiteSpace(currentDisplayName))
		{
			return currentDisplayName;
		}

		var normalizedKey = targetKey?.Trim().ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(normalizedKey))
		{
			return currentDisplayName;
		}

		if (string.Equals(targetType, "employee", StringComparison.OrdinalIgnoreCase) &&
			employeeNames.TryGetValue(normalizedKey, out var employeeName))
		{
			return employeeName;
		}

		if (string.Equals(targetType, "group", StringComparison.OrdinalIgnoreCase) &&
			groupNames.TryGetValue(normalizedKey, out var groupName))
		{
			return groupName;
		}

		return currentDisplayName;
	}

	public static string BuildEmployeeDisplayName(Employee employee)
	{
		if (!string.IsNullOrWhiteSpace(employee.EmployeeName))
		{
			return employee.EmployeeName;
		}

		var name = string.Join(" ", new[] { employee.FirstName, employee.MiddleName, employee.LastName }
			.Where(part => !string.IsNullOrWhiteSpace(part)));
		return string.IsNullOrWhiteSpace(name) ? employee.EmployeeCode : name;
	}

	public static object BuildAssignmentAuditContext(HrPolicyAssignment assignment) => new
	{
		targetType = HrPolicyMapping.MapAssignmentTargetType(assignment.TargetType),
		assignment.TargetKey,
		assignment.TargetDisplayName,
		assignment.PayrollPolicyId,
		assignment.AttendancePolicyId,
		assignment.EffectiveFrom,
		assignment.EffectiveTo,
		assignment.Priority,
		assignment.IsActive
	};
}
