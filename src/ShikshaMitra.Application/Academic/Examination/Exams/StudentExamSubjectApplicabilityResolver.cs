using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Students;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Examination.Exams;

public class StudentExamSubjectApplicabilityResolver(
    IRepository<AssignedSubject, Guid> assignedSubjectRepository,
    IRepository<StudentElectiveSelection, Guid> studentElectiveSelectionRepository)
    : ITransientDependency
{
    public async Task<Dictionary<Guid, HashSet<Guid>>> ResolveAsync(
        Exam exam,
        IReadOnlyCollection<Student> students)
    {
        var examSubjectIds = exam.ExamSubjects.Select(subject => subject.SubjectId).ToHashSet();
        var result = students.ToDictionary(student => student.Id, _ => new HashSet<Guid>());
        if (students.Count == 0 || examSubjectIds.Count == 0)
        {
            return result;
        }

        var electiveAssignments = await assignedSubjectRepository.GetListAsync(assignment =>
            assignment.StandardId == exam.StandardId &&
            examSubjectIds.Contains(assignment.SubjectId) &&
            assignment.ElectiveGroupId.HasValue);
        var electiveGroupBySubjectId = electiveAssignments
            .GroupBy(assignment => assignment.SubjectId)
            .ToDictionary(group => group.Key, group => group.First().ElectiveGroupId!.Value);
        if (electiveGroupBySubjectId.Count == 0)
        {
            foreach (var subjectIds in result.Values)
            {
                subjectIds.UnionWith(examSubjectIds);
            }

            return result;
        }

        var studentIds = students.Select(student => student.Id).ToHashSet();
        var electiveGroupIds = electiveGroupBySubjectId.Values.ToHashSet();
        var selections = await studentElectiveSelectionRepository.GetListAsync(selection =>
            studentIds.Contains(selection.StudentId) &&
            electiveGroupIds.Contains(selection.ElectiveGroupId) &&
            selection.IsLocked);
        var selectionByStudentAndGroup = selections
            .GroupBy(selection => (selection.StudentId, selection.ElectiveGroupId))
            .ToDictionary(group => group.Key, group => group.First().SelectedSubjectId);
        var examSubjectIdsByElectiveGroup = electiveGroupBySubjectId
            .GroupBy(entry => entry.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.Key).ToHashSet());

        foreach (var student in students)
        {
            var applicableSubjectIds = result[student.Id];
            foreach (var examSubjectId in examSubjectIds)
            {
                if (!electiveGroupBySubjectId.TryGetValue(examSubjectId, out var electiveGroupId))
                {
                    applicableSubjectIds.Add(examSubjectId);
                    continue;
                }

                if (!selectionByStudentAndGroup.TryGetValue((student.Id, electiveGroupId), out var selectedSubjectId) ||
                    !examSubjectIdsByElectiveGroup[electiveGroupId].Contains(selectedSubjectId))
                {
                    // Fail closed for incomplete or stale elective configuration so readiness
                    // reports missing data instead of silently omitting scheduled subjects.
                    applicableSubjectIds.Add(examSubjectId);
                    continue;
                }

                if (selectedSubjectId == examSubjectId)
                {
                    applicableSubjectIds.Add(examSubjectId);
                }
            }
        }

        return result;
    }
}
