using Volo.Abp.Domain.Repositories;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task ConvertLegacyPolicyAssignmentsToGroupsAsync()
	{
		var assignments = (await _hrPolicyAssignmentRepository.GetQueryableAsync())
			.AsEnumerable()
			.Where(x => (int)x.TargetType is 2 or 3 or 4 or 5)
			.ToList();
		if (assignments.Count == 0)
		{
			return;
		}

		var employees = (await _employeeRepository.GetQueryableAsync()).AsEnumerable().ToList();
		foreach (var assignment in assignments)
		{
			var legacyType = (int)assignment.TargetType;
			var displayName = assignment.TargetDisplayName ?? assignment.TargetKey;
			var group = legacyType switch
			{
				2 => await EnsureLegacyShiftGroupAsync(assignment.AcademicYearId, displayName, assignment.TargetKey, employees),
				3 => await EnsureLegacyRoleGroupAsync(assignment.AcademicYearId, displayName, assignment.TargetKey),
				4 => await EnsureLegacyEmployeeRuleGroupAsync(assignment.AcademicYearId, displayName, EmployeeRuleField.Department, assignment.TargetKey),
				5 => await EnsureLegacyEmployeeRuleGroupAsync(assignment.AcademicYearId, displayName, EmployeeRuleField.EmploymentType, assignment.TargetKey),
				_ => null
			};

			if (group == null)
			{
				continue;
			}

			assignment.UpdateTarget(HrPolicyAssignmentTargetType.Group, group.Id.ToString("D"), group.Name);
			await _hrPolicyAssignmentRepository.UpdateAsync(assignment, autoSave: false);
		}

		await CurrentUnitOfWork.SaveChangesAsync();
		_logger.LogInformation("[HRSeeder] Converted {Count} legacy HR policy assignments to group assignments.", assignments.Count);
	}

	private async Task<Group> EnsureLegacyEmployeeRuleGroupAsync(
		Guid academicYearId,
		string displayName,
		EmployeeRuleField field,
		string value)
	{
		var name = BuildLegacyPolicyGroupName(displayName);
		var existing = await GetExistingGroupWithDetailsAsync(academicYearId, name);
		if (existing != null)
		{
			return existing;
		}

		var group = Group.Create(GuidGenerator.Create(), CurrentTenant.Id, academicYearId, name, "Migrated HR policy assignment group.", [], [], [], []);
		group.AddEmployeeRule(new GroupEmployeeRule(GuidGenerator.Create(), group.Id, GroupSelectionType.RuleBased, field, RuleOperator.Equals, value));
		var employees = (await _employeeRepository.GetQueryableAsync()).AsEnumerable().ToList();
		AddMissingEmployeeMembers(group, employees);
		await _groupRepository.InsertAsync(group, autoSave: false);
		return group;
	}

	private async Task<Group?> EnsureLegacyRoleGroupAsync(Guid academicYearId, string displayName, string roleIdText)
	{
		if (!Guid.TryParse(roleIdText, out var roleId))
		{
			return null;
		}

		var name = BuildLegacyPolicyGroupName(displayName);
		var existing = await GetExistingGroupWithDetailsAsync(academicYearId, name);
		if (existing != null)
		{
			return existing;
		}

		var group = Group.Create(GuidGenerator.Create(), CurrentTenant.Id, academicYearId, name, "Migrated HR role policy assignment group.", [], [roleId], [], []);
		var employees = (await _employeeRepository.GetQueryableAsync()).AsEnumerable().ToList();
		AddMissingEmployeeMembers(group, employees);
		await _groupRepository.InsertAsync(group, autoSave: false);
		return group;
	}

	private async Task<Group?> EnsureLegacyShiftGroupAsync(
		Guid academicYearId,
		string displayName,
		string shiftIdText,
		IReadOnlyList<Employee> employees)
	{
		if (!Guid.TryParse(shiftIdText, out var shiftId))
		{
			return null;
		}

		var name = BuildLegacyPolicyGroupName(displayName);
		var existing = await GetExistingGroupWithDetailsAsync(academicYearId, name);
		if (existing != null)
		{
			return existing;
		}

		var group = Group.Create(GuidGenerator.Create(), CurrentTenant.Id, academicYearId, name, "Migrated static HR shift policy assignment group.", [], [], [], []);
		foreach (var employee in employees.Where(x => x.ShiftId == shiftId))
		{
			group.AddMember(new GroupMember(GuidGenerator.Create(), group.Id, GroupMemberType.Employee, employee.Id, employee.UserId));
		}
		await _groupRepository.InsertAsync(group, autoSave: false);
		return group;
	}

	private static string BuildLegacyPolicyGroupName(string displayName) =>
		$"HR Policy - {displayName}".Trim();

	private async Task<int> EnsurePolicyAssignmentAsync(
		AcademicYear academicYear,
		HrPolicyAssignmentTargetType targetType,
		string targetKey,
		string targetDisplayName,
		PayrollPolicy payrollPolicy,
		AttendancePolicy attendancePolicy,
		int priority,
		string reason)
	{
		var normalizedTargetKey = HrPolicyAssignment.NormalizeTargetKey(targetType, targetKey);
		var effectiveFrom = academicYear.StartDate.Date;
		var effectiveTo = academicYear.EndDate.Date;
		var existing = await _hrPolicyAssignmentRepository.FirstOrDefaultAsync(x =>
			x.AcademicYearId == academicYear.Id &&
			x.TargetType == targetType &&
			x.TargetKey == normalizedTargetKey &&
			x.EffectiveFrom == effectiveFrom &&
			x.EffectiveTo == effectiveTo);
		if (existing != null)
		{
			var needsUpdate =
				existing.AttendancePolicyId != attendancePolicy.Id ||
				existing.PayrollPolicyId != payrollPolicy.Id ||
				existing.Priority != priority ||
				!existing.IsActive ||
				!string.Equals(existing.AssignmentReason, reason, StringComparison.Ordinal) ||
				!string.Equals(existing.TargetDisplayName, targetDisplayName, StringComparison.Ordinal);
			if (needsUpdate)
			{
				existing.UpdateTarget(targetType, normalizedTargetKey, targetDisplayName);
				existing.Update(
					attendancePolicy.Id,
					payrollPolicy.Id,
					effectiveFrom,
					effectiveTo,
					isActive: true,
					priority,
					reason);
				await _hrPolicyAssignmentRepository.UpdateAsync(existing, autoSave: false);
			}

			return 0;
		}

		var assignment = new HrPolicyAssignment(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYear.Id,
			targetType,
			normalizedTargetKey,
			targetDisplayName,
			attendancePolicy.Id,
			payrollPolicy.Id,
			effectiveFrom,
			effectiveTo,
			isActive: true,
			priority,
			reason);
		await _hrPolicyAssignmentRepository.InsertAsync(assignment, autoSave: false);
		return 1;
	}
}
