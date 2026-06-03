using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Groups;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task SeedDemoHrPolicyAssignmentsAsync(List<Guid> activeEmployeeIds)
	{
		await ConvertLegacyPolicyAssignmentsToGroupsAsync();

		if (!ShouldSeedDemoPolicyScenarios())
		{
			return;
		}

		var currentStartYear = _clock.Now.Year;
		var academicYears = (await _academicYearRepository.GetListAsync())
			.Where(x =>
				x.Name.StartsWith((currentStartYear - 1).ToString(), StringComparison.OrdinalIgnoreCase) ||
				x.Name.StartsWith(currentStartYear.ToString(), StringComparison.OrdinalIgnoreCase))
			.OrderBy(x => x.StartDate)
			.ToList();
		if (academicYears.Count == 0)
		{
			return;
		}

		var employees = (await _employeeRepository.GetQueryableAsync())
			.AsEnumerable()
			.Where(e => activeEmployeeIds.Count == 0 || activeEmployeeIds.Contains(e.Id))
			.Where(IsSeededDemoEmployee)
			.ToList();
		var shifts = await _shiftRepository.GetListAsync();
		var insertedCount = 0;
		var updatedGroupMemberCount = 0;

		foreach (var academicYear in academicYears)
		{
			var standardPayroll = await _payrollPolicyRepository.FirstOrDefaultAsync(x =>
				x.AcademicYearId == academicYear.Id &&
				x.Name == "Standard Payroll Policy");
			var operationsPayroll = await _payrollPolicyRepository.FirstOrDefaultAsync(x =>
				x.AcademicYearId == academicYear.Id &&
				x.Name == "Operations Payroll Policy");
			var partTimePayroll = await _payrollPolicyRepository.FirstOrDefaultAsync(x =>
				x.AcademicYearId == academicYear.Id &&
				x.Name == "Part-Time Payroll Policy");
			var standardAttendance = await _attendancePolicyRepository.FirstOrDefaultAsync(x =>
				x.AcademicYearId == academicYear.Id &&
				x.Name == "Standard Attendance Policy");
			var operationsAttendance = await _attendancePolicyRepository.FirstOrDefaultAsync(x =>
				x.AcademicYearId == academicYear.Id &&
				x.Name == "Operations Attendance Policy");
			var partTimeAttendance = await _attendancePolicyRepository.FirstOrDefaultAsync(x =>
				x.AcademicYearId == academicYear.Id &&
				x.Name == "Part-Time Attendance Policy");

			if (standardPayroll == null || operationsPayroll == null || partTimePayroll == null ||
			    standardAttendance == null || operationsAttendance == null || partTimeAttendance == null)
			{
				continue;
			}

			var teachingGroup = await EnsureEmployeeRuleGroupAsync(
				academicYear,
				"Teaching Staff",
				"HR policy group for teaching employees.",
				EmployeeRuleField.EmploymentType,
				EmployeeGroupType.Teaching.ToString());
			updatedGroupMemberCount += await EnsureGroupEmployeeMembersAsync(teachingGroup, employees);
			insertedCount += await EnsurePolicyAssignmentAsync(
				academicYear,
				HrPolicyAssignmentTargetType.Group,
				teachingGroup.Id.ToString("D"),
				teachingGroup.Name,
				standardPayroll,
				standardAttendance,
				priority: 10,
				"Default seeded teaching staff policy.");

			var nonTeachingGroup = await EnsureEmployeeRuleGroupAsync(
				academicYear,
				"Non-Teaching Staff",
				"HR policy group for non-teaching employees.",
				EmployeeRuleField.EmploymentType,
				EmployeeGroupType.NonTeaching.ToString());
			updatedGroupMemberCount += await EnsureGroupEmployeeMembersAsync(nonTeachingGroup, employees);
			insertedCount += await EnsurePolicyAssignmentAsync(
				academicYear,
				HrPolicyAssignmentTargetType.Group,
				nonTeachingGroup.Id.ToString("D"),
				nonTeachingGroup.Name,
				operationsPayroll,
				operationsAttendance,
				priority: 10,
				"Default seeded non-teaching operations policy.");

			var administrationGroup = await EnsureEmployeeRuleGroupAsync(
				academicYear,
				"Administration Department",
				"HR policy group for administration employees.",
				EmployeeRuleField.Department,
				"Administration");
			updatedGroupMemberCount += await EnsureGroupEmployeeMembersAsync(administrationGroup, employees);
			insertedCount += await EnsurePolicyAssignmentAsync(
				academicYear,
				HrPolicyAssignmentTargetType.Group,
				administrationGroup.Id.ToString("D"),
				administrationGroup.Name,
				operationsPayroll,
				operationsAttendance,
				priority: 30,
				"Administration department uses operations payroll and attendance rules.");

			var teacherRole = await FindRoleSeedInfoAsync("Teacher");
			if (teacherRole != null)
			{
				var teacherRoleGroup = await EnsureEmployeeRoleGroupAsync(
					academicYear,
					"Teacher Role",
					"HR policy group for employees in the Teacher role.",
					teacherRole.Id);
				updatedGroupMemberCount += await EnsureGroupEmployeeMembersAsync(teacherRoleGroup, employees);
				insertedCount += await EnsurePolicyAssignmentAsync(
					academicYear,
					HrPolicyAssignmentTargetType.Group,
					teacherRoleGroup.Id.ToString("D"),
					teacherRoleGroup.Name,
					standardPayroll,
					standardAttendance,
					priority: 20,
					"Teacher role seeded policy example.");
			}

			var morningShift = shifts.FirstOrDefault(x => x.ShiftName == "Morning Shift");
			if (morningShift != null)
			{
				var morningShiftGroup = await EnsureManualEmployeeGroupAsync(
					academicYear,
					"Morning Shift",
					"Static HR policy group seeded from current morning-shift employees.",
					employees.Where(employee => employee.ShiftId == morningShift.Id));
				updatedGroupMemberCount += await EnsureGroupEmployeeMembersAsync(
					morningShiftGroup,
					employees.Where(employee => employee.ShiftId == morningShift.Id));
				insertedCount += await EnsurePolicyAssignmentAsync(
					academicYear,
					HrPolicyAssignmentTargetType.Group,
					morningShiftGroup.Id.ToString("D"),
					morningShiftGroup.Name,
					partTimePayroll,
					partTimeAttendance,
					priority: 40,
					"Morning shift seeded part-time policy example.");
			}

			var directEmployee = employees.FirstOrDefault(x =>
					x.Email?.Equals("raj.baku@ddvs.com", StringComparison.OrdinalIgnoreCase) == true)
				?? employees.FirstOrDefault();
			if (directEmployee != null)
			{
				insertedCount += await EnsurePolicyAssignmentAsync(
					academicYear,
					HrPolicyAssignmentTargetType.Employee,
					directEmployee.Id.ToString("D"),
					$"{directEmployee.FirstName} {directEmployee.LastName}".Trim(),
					partTimePayroll,
					partTimeAttendance,
					priority: 100,
					"Seeded direct employee override for policy precedence testing.");
			}
		}

		if (insertedCount > 0 || updatedGroupMemberCount > 0)
		{
			await CurrentUnitOfWork.SaveChangesAsync();
		}

		_logger.LogInformation("[HRSeeder] Demo HR policy assignments inserted={InsertedCount}, group employee members added={UpdatedGroupMemberCount}", insertedCount, updatedGroupMemberCount);
	}
}
