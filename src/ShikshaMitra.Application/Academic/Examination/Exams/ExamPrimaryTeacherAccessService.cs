using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.HR.Employees;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Examination.Exams;

/// <summary>
/// Resolves the exam access derived from published primary-teacher subject assignments.
/// Centralizes the exam-subject ownership rule for result/report endpoints so they stay
/// aligned with the exam selection rule.
/// </summary>
public class ExamPrimaryTeacherAccessService(
    IRepository<Employee, Guid> employeeRepository,
    IRepository<AssignedSubject, Guid> assignedSubjectRepository,
    IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
    public async Task<bool> HasAccessAsync(Guid userId, Exam exam)
    {
        var requestedSubjectIds = exam.ExamSubjects
            .Select(subject => subject.SubjectId)
            .Where(subjectId => subjectId != Guid.Empty)
            .ToHashSet();
        if (requestedSubjectIds.Count == 0)
        {
            return false;
        }

        var employee = await employeeRepository.FirstOrDefaultAsync(
            candidate => candidate.UserId == userId);
        if (employee == null)
        {
            return false;
        }

        var assignmentsWithTeachers = await assignedSubjectRepository.WithDetailsAsync(
            assignment => assignment.Teachers);
        var assignedSubjectIds = await asyncExecuter.ToListAsync(
            assignmentsWithTeachers
                .Where(assignment =>
                    assignment.StandardId == exam.StandardId &&
                    assignment.Status == SubjectStatus.Published &&
                    requestedSubjectIds.Contains(assignment.SubjectId) &&
                    assignment.Teachers.Any(teacher =>
                        teacher.TeacherType == SubjectTeacherType.Primary &&
                        teacher.TeacherId == employee.Id))
                .Select(assignment => assignment.SubjectId)
                .Distinct());

        return requestedSubjectIds.IsSubsetOf(assignedSubjectIds);
    }
}
