using Volo.Abp.Domain.Repositories;
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
	private static readonly Lazy<IReadOnlySet<string>> SeededDemoEmployeeEmails = new(
		() => BuildSuratSeedEmployeeProfiles()
			.Keys
			.ToHashSet(StringComparer.OrdinalIgnoreCase));

	private async Task<HashSet<DateOnly>> BuildSeedHolidayDateSetAsync(
		DateOnly windowStart,
		DateOnly windowEnd,
		Guid? academicYearId = null)
	{
		if (windowEnd < windowStart)
		{
			return [];
		}

		var holidays = await _holidayRepository.GetListAsync(x =>
			!academicYearId.HasValue || x.AcademicYearId == academicYearId.Value);
		var ranges = holidays
			.Select(x => new SeedHolidayDateRange(
				x.StartDate,
				x.EndDate))
			.Where(x => x.EndDate >= windowStart && x.StartDate <= windowEnd)
			.ToList();

		return BuildSeedHolidayDateSet(ranges, windowStart, windowEnd);
	}

	private async Task<Group> EnsureEmployeeRuleGroupAsync(
		AcademicYear academicYear,
		string name,
		string description,
		EmployeeRuleField field,
		string value)
	{
		var existing = await GetExistingGroupWithDetailsAsync(academicYear.Id, name);
		if (existing != null)
		{
			return existing;
		}

		var group = Group.Create(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYear.Id,
			name,
			description,
			[],
			[],
			[],
			[]);
		group.AddEmployeeRule(new GroupEmployeeRule(
			GuidGenerator.Create(),
			group.Id,
			GroupSelectionType.RuleBased,
			field,
			RuleOperator.Equals,
			value));
		AddMissingEmployeeMembers(group, employeesForGroup: null);
		await _groupRepository.InsertAsync(group, autoSave: false);
		return group;
	}

	private async Task<Group> EnsureEmployeeRoleGroupAsync(
		AcademicYear academicYear,
		string name,
		string description,
		Guid roleId)
	{
		var existing = await GetExistingGroupWithDetailsAsync(academicYear.Id, name);
		if (existing != null)
		{
			return existing;
		}

		var group = Group.Create(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYear.Id,
			name,
			description,
			[],
			[roleId],
			[],
			[]);
		AddMissingEmployeeMembers(group, employeesForGroup: null);
		await _groupRepository.InsertAsync(group, autoSave: false);
		return group;
	}

	private async Task<Group> EnsureManualEmployeeGroupAsync(
		AcademicYear academicYear,
		string name,
		string description,
		IEnumerable<Employee> employees)
	{
		var existing = await GetExistingGroupWithDetailsAsync(academicYear.Id, name);
		if (existing != null)
		{
			return existing;
		}

		var group = Group.Create(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYear.Id,
			name,
			description,
			[],
			[],
			[],
			[]);
		foreach (var employee in employees)
		{
			group.AddMember(new GroupMember(
				GuidGenerator.Create(),
				group.Id,
				GroupMemberType.Employee,
				employee.Id,
				employee.UserId));
		}
		await _groupRepository.InsertAsync(group, autoSave: false);
		return group;
	}

	private async Task<Group?> GetExistingGroupWithDetailsAsync(Guid academicYearId, string name)
	{
		var existing = await _groupRepository.FirstOrDefaultAsync(x =>
			x.AcademicYearId == academicYearId &&
			x.Name == name);
		return existing == null ? null : await _groupRepository.GetWithDetailsAsync(existing.Id);
	}

	private async Task<int> EnsureGroupEmployeeMembersAsync(Group group, IEnumerable<Employee> candidateEmployees)
	{
		var addedCount = AddMissingEmployeeMembers(group, candidateEmployees);
		if (addedCount == 0)
		{
			return 0;
		}

		await _groupRepository.UpdateAsync(group, autoSave: false);
		return addedCount;
	}

	private int AddMissingEmployeeMembers(Group group, IEnumerable<Employee>? employeesForGroup)
	{
		var candidates = employeesForGroup?.ToList();
		if (candidates == null)
		{
			return 0;
		}

		var hasDynamicEmployeeSelection = group.RuleBasedRoleIds.Count > 0 || group.GetRuleBasedEmployeeRules().Any();
		var matchingEmployeeIds = hasDynamicEmployeeSelection
			? HrPolicyGroupMembership.ResolveEmployeeIds(group, candidates).ToHashSet()
			: candidates.Select(employee => employee.Id).ToHashSet();
		var existingEmployeeIds = group.Members
			.Where(member => member.MemberType == GroupMemberType.Employee)
			.Select(member => member.MemberId)
			.ToHashSet();
		var addedCount = 0;

		foreach (var employee in candidates.Where(employee => matchingEmployeeIds.Contains(employee.Id)))
		{
			if (!existingEmployeeIds.Add(employee.Id))
			{
				continue;
			}

			group.AddMember(new GroupMember(
				GuidGenerator.Create(),
				group.Id,
				GroupMemberType.Employee,
				employee.Id,
				employee.UserId));
			addedCount++;
		}

		return addedCount;
	}

	internal static bool IsSeededDemoEmployeeEmail(string? email) =>
		!string.IsNullOrWhiteSpace(email) &&
		SeededDemoEmployeeEmails.Value.Contains(email);

	private static bool IsSeededDemoEmployee(Employee employee) =>
		IsSeededDemoEmployeeEmail(employee.Email);

}
