using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Examination.Exams;

public partial class ExamAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View)]
    public async Task<List<string>> GetGradeOptionsAsync(GetExamGradeOptionsInput input)
    {
        using var examTracking = examRepository.DisableTracking();
        using var gradeTracking = gradingScaleRepository.DisableTracking();
        var examQuery = await examRepository.WithDetailsAsync(exam => exam.ExamSubjects, exam => exam.ExamDivisions);
        var exam = await AsyncExecuter.FirstOrDefaultAsync(examQuery.Where(exam => exam.Id == input.ExamId))
            ?? throw new EntityNotFoundException(typeof(Exam), input.ExamId);
        await EnsureCanAccessExamAsync(exam);
        if (input.StandardId.HasValue && input.StandardId.Value != exam.StandardId) return [];
        var subjects = exam.ExamSubjects.Where(subject => !input.SubjectId.HasValue || subject.SubjectId == input.SubjectId.Value)
            .GroupBy(subject => subject.SubjectId).Select(group => group.First()).Where(subject => (subject.TotalMarks ?? 0) != 0).ToList();
        var divisionIds = exam.ExamDivisions.Select(division => division.DivisionId).Distinct()
            .Where(id => !input.DivisionId.HasValue || id == input.DivisionId.Value).ToList();
        var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
        if (!scope.IsAdmin && divisionIds.Count > 0)
        {
            var (stageId, mediumId) = await GetStandardScopeIdsAsync(exam.StandardId);
            var permitted = divisionIds.Where(id => CanAccessExam(exam.StandardId, stageId, mediumId, [id], scope)).ToList();
            if (permitted.Count != divisionIds.Count && !await CurrentUserIsPrimaryTeacherForAllSubjectsAsync(
                    exam.StandardId, exam.ExamSubjects.Select(subject => subject.SubjectId).ToList())) divisionIds = permitted;
        }
        if (subjects.Count == 0 || divisionIds.Count == 0) return [];
        var subjectIds = subjects.Select(subject => subject.SubjectId).ToList();
        var assignments = await AsyncExecuter.ToListAsync((await assignmentRepository.GetQueryableAsync())
            .Where(assignment => assignment.StandardId == exam.StandardId && subjectIds.Contains(assignment.SubjectId) && assignment.ElectiveGroupId.HasValue)
            .Select(assignment => new { assignment.SubjectId, GroupId = assignment.ElectiveGroupId!.Value }));
        var groupsBySubject = assignments.GroupBy(assignment => assignment.SubjectId)
            .ToDictionary(group => group.Key, group => group.First().GroupId);
        var eligibleStudents = (await studentRepository.GetQueryableAsync()).Where(student => student.DivisionId.HasValue &&
            divisionIds.Contains(student.DivisionId.Value) && student.Status == StudentLifeCycleStatusEnum.Enrolled &&
            student.AdmissionApprovalStatus == StudentEnrollmentApprovalState.Approved && !student.IsDeleted).Select(student => student.Id);
        var marks = (await examMarkRepository.GetQueryableAsync()).Where(mark => mark.ExamId == exam.Id &&
            subjectIds.Contains(mark.SubjectId) && divisionIds.Contains(mark.DivisionId) && mark.MarksObtained.HasValue && eligibleStudents.Contains(mark.StudentId));
        var conditions = new List<Expression<Func<ExamMark, bool>>>();
        var coreIds = subjectIds.Where(id => !groupsBySubject.ContainsKey(id)).ToList();
        if (coreIds.Count > 0) conditions.Add(mark => coreIds.Contains(mark.SubjectId));
        var selections = (await studentElectiveSelectionRepository.GetQueryableAsync()).Where(selection => selection.IsLocked);
        foreach (var group in groupsBySubject.GroupBy(entry => entry.Value))
        {
            var groupId = group.Key;
            var ids = group.Select(entry => entry.Key).ToList();
            conditions.Add(mark => ids.Contains(mark.SubjectId) && selections.Any(selection => selection.ElectiveGroupId == groupId &&
                selection.StudentId == mark.StudentId && selection.SelectedSubjectId == mark.SubjectId));
        }
        var parameter = Expression.Parameter(typeof(ExamMark), "mark");
        var bodies = conditions.Select(condition => new GradeOptionParameterReplacer(condition.Parameters[0], parameter).Visit(condition.Body)!).ToList();
        var predicate = Expression.Lambda<Func<ExamMark, bool>>(CombineGradeOptionConditions(bodies, 0, bodies.Count), parameter);
        var values = await AsyncExecuter.ToListAsync(marks.Where(predicate).Select(mark => new { mark.SubjectId, Marks = mark.MarksObtained!.Value }).Distinct());
        if (values.Count == 0) return [];
        var scaleIds = subjects.Where(subject => subject.GradeScalingId.HasValue).Select(subject => subject.GradeScalingId!.Value).Distinct().ToList();
        var scales = scaleIds.Count == 0 ? new List<GradingScale>() : await AsyncExecuter.ToListAsync(
            (await gradingScaleRepository.WithDetailsAsync(scale => scale.GradeRanges)).Where(scale => scaleIds.Contains(scale.Id)));
        var scaleMap = scales.ToDictionary(scale => scale.Id);
        var definitions = new Dictionary<Guid, GradeOptionDefinition>();
        foreach (var subject in subjects)
        {
            var scale = subject.GradeScalingId.HasValue ? scaleMap.GetValueOrDefault(subject.GradeScalingId.Value) : null;
            var scaleTotal = 100.0;
            if (scale != null && double.TryParse(scale.TotalMarks, out var parsedTotal)) scaleTotal = parsedTotal;
            definitions[subject.SubjectId] = new(subject.TotalMarks ?? 0,
                scale?.GradeRanges.OrderByDescending(range => range.MinMarks).ToList() ?? [], scaleTotal);
        }
        return values.Select(value => GradeOptionLabel(value.Marks, definitions[value.SubjectId]))
            .Where(label => !string.IsNullOrWhiteSpace(label)).Select(label => label!.Trim())
            .Distinct(StringComparer.Ordinal).OrderBy(label => label, StringComparer.Ordinal).ToList();
    }

    private static string? GradeOptionLabel(double marks, GradeOptionDefinition definition)
    {
        if (definition.Total == 0) return null;
        if (definition.Ranges.Count > 0)
        {
            var scaled = marks / definition.Total * definition.ScaleTotal;
            return definition.Ranges.FirstOrDefault(range => scaled >= range.MinMarks && scaled <= range.MaxMarks)?.Grade;
        }
        var percentage = marks / definition.Total * 100;
        return percentage >= 90 ? "A+" : percentage >= 80 ? "A" : percentage >= 70 ? "B+" : percentage >= 60 ? "B"
            : percentage >= 50 ? "C+" : percentage >= 40 ? "C" : percentage >= 33 ? "D" : "F";
    }

    private static Expression CombineGradeOptionConditions(List<Expression> conditions, int start, int count)
    {
        if (count == 1) return conditions[start];
        var left = count / 2;
        return Expression.OrElse(CombineGradeOptionConditions(conditions, start, left), CombineGradeOptionConditions(conditions, start + left, count - left));
    }

    private sealed record GradeOptionDefinition(double Total, IReadOnlyList<GradeRange> Ranges, double ScaleTotal);

    private sealed class GradeOptionParameterReplacer(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == source ? target : base.VisitParameter(node);
    }
}
