using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Curriculum;

public partial class SubjectAssignmentAppService
{
	private async Task<List<List<Guid>>> GetAssignmentListGroupKeysAsync(
		IQueryable<AssignedSubject> query, string? teacherSearch)
	{
		var keys = await AsyncExecuter.ToListAsync(query.OrderBy(assignment => assignment.Id)
			.Select(assignment => new
			{
				assignment.Id,
				assignment.SubjectType,
				assignment.ElectiveGroupId
			}));
		var groups = keys
			.Where(assignment => assignment.SubjectType == SubjectType.Core ||
				(assignment.SubjectType == SubjectType.Elective && !assignment.ElectiveGroupId.HasValue))
			.Select(assignment => new List<Guid> { assignment.Id })
			.Concat(keys.Where(assignment => assignment.SubjectType == SubjectType.Elective && assignment.ElectiveGroupId.HasValue)
				.GroupBy(assignment => assignment.ElectiveGroupId)
				.Select(group => group.Select(assignment => assignment.Id).ToList()))
			.ToList();
		var search = teacherSearch?.Trim();
		if (string.IsNullOrWhiteSpace(search) || groups.Count == 0)
		{
			return groups;
		}

		var teachers = await AsyncExecuter.ToListAsync(query.SelectMany(assignment => assignment.Teachers)
			.Select(teacher => new { teacher.AssignedSubjectId, teacher.TeacherId, teacher.TeacherType }));
		var teacherIds = teachers.Select(teacher => teacher.TeacherId).Distinct().ToList();
		var (legacyNames, displayNames) = await LoadAssignmentListTeacherNamesAsync(teacherIds);
		var matchingAssignments = teachers.Where(teacher =>
		{
			var displayName = displayNames.GetValueOrDefault(teacher.TeacherId) ?? legacyNames.GetValueOrDefault(teacher.TeacherId);
			var primaryName = teacher.TeacherType == SubjectTeacherType.Primary ? legacyNames.GetValueOrDefault(teacher.TeacherId) : null;
			return (!string.IsNullOrWhiteSpace(displayName) && displayName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
				(!string.IsNullOrWhiteSpace(primaryName) && primaryName.Contains(search, StringComparison.OrdinalIgnoreCase));
		}).Select(teacher => teacher.AssignedSubjectId).ToHashSet();
		return groups.Where(group => group.Any(matchingAssignments.Contains)).ToList();
	}

	private async Task<(Dictionary<Guid, string> LegacyNames, Dictionary<Guid, string> DisplayNames)> LoadAssignmentListTeacherNamesAsync(
		List<Guid> teacherIds)
	{
		if (teacherIds.Count == 0)
		{
			return (new Dictionary<Guid, string>(), new Dictionary<Guid, string>());
		}
		var users = await AsyncExecuter.ToListAsync((await identityUserQueryRepository.GetQueryableAsync())
			.Where(user => teacherIds.Contains(user.Id)).Select(user => new { user.Id, user.Name, user.Surname }));
		var legacyNames = users.ToDictionary(user => user.Id, user => $"{user.Name} {user.Surname}".Trim());
		var employees = await AsyncExecuter.ToListAsync((await employeeRepository.GetQueryableAsync())
			.Where(employee => teacherIds.Contains(employee.Id))
			.Select(employee => new { employee.Id, employee.EmployeeCode, employee.FirstName, employee.LastName }));
		var displayNames = employees.ToDictionary(employee => employee.Id, employee =>
		{
			var name = string.Join(" ", new[] { employee.FirstName, employee.LastName }
				.Where(part => !string.IsNullOrWhiteSpace(part)));
			return string.IsNullOrWhiteSpace(employee.EmployeeCode) ? name : $"{employee.EmployeeCode} - {name}";
		});
		return (legacyNames, displayNames);
	}
}
