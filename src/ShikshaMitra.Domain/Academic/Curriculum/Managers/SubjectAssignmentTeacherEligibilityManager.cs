using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectAssignmentTeacherEligibilityManager(
	IUserScopeAssignmentRepository scopeRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<Standard, Guid> standardRepository) : DomainService
{
	public async Task EnsureEligibleAsync(Guid standardId, IReadOnlyDictionary<Guid, List<Guid>> teachersBySubject)
	{
		var teacherIds = teachersBySubject.Values.SelectMany(ids => ids).Distinct().ToArray();
		if (teacherIds.Length == 0) return;

		var eligibleUserIds = (await scopeRepository.GetUserIdsByStandardAsync(standardId)).ToHashSet();
		var teachers = (await employeeRepository.GetListAsync(employee => Enumerable.Contains(teacherIds, employee.Id)))
			.ToDictionary(employee => employee.Id);
		foreach (var subject in teachersBySubject)
		{
			foreach (var teacherId in subject.Value)
			{
				if (teachers.TryGetValue(teacherId, out var teacher) &&
					teacher.UserId.HasValue && eligibleUserIds.Contains(teacher.UserId.Value)) continue;

				var standard = await standardRepository.GetAsync(standardId);
				var teacherName = teacher == null ? "The selected teacher" : $"{teacher.FirstName} {teacher.LastName}".Trim();
				throw new BusinessException(CurriculumErrorCodes.SubjectAssignmentTeacherNotInClass)
					.WithData("TeacherName", teacherName)
					.WithData("ClassName", standard.Name)
					.WithData("TeacherId", teacherId)
					.WithData("SubjectId", subject.Key)
					.WithData("Field", "teacherIds");
			}
		}
	}
}
