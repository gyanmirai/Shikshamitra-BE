using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportGenerator
{
    private async Task<AcademicReportPreviewDto?> TryBuildStudentExamPreviewAsync(AcademicReportRequestDto request, AcademicReportPreviewDto payload)
    {
        if (request.ReportType is not (AcademicReportTypes.StudentAllBatchResult or AcademicReportTypes.StudentAllExamResult)) return null;
        payload.Columns = CreateStudentExamExport(request, 256, CancellationToken.None).Metadata.Columns;
        var studentIds = request.Filters.StudentIds;
        if (studentIds is not { Count: > 0 }) return payload;
        var allBatch = request.ReportType == AcademicReportTypes.StudentAllBatchResult;
        var marks = await examMarkRepository.GetQueryableAsync();
        var target = marks.Where(mark => studentIds.Contains(mark.StudentId));
        if (request.Filters.DivisionIds is { Count: > 0 }) target = target.Where(mark => request.Filters.DivisionIds.Contains(mark.DivisionId));
        if (!await asyncExecuter.AnyAsync(target)) return payload;
        var exams = (await examRepository.GetQueryableAsync()).Where(exam => target.Any(mark => mark.ExamId == exam.Id));
        if (request.Filters.ClassIds is { Count: > 0 }) exams = exams.Where(exam => request.Filters.ClassIds.Contains(exam.StandardId));
        var metadata = await asyncExecuter.ToListAsync(exams.Select(exam => new { exam.Id, exam.ExamName, exam.StandardId }));
        if (metadata.Count == 0) return payload;
        var examMap = metadata.ToDictionary(exam => exam.Id);
        var students = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
        var names = allBatch ? await asyncExecuter.ToListAsync(students.Where(student => studentIds.Contains(student.Id))
            .Select(student => new { student.Id, student.FirstName, student.MiddleName, student.LastName })) : [];
        var studentNames = names.ToDictionary(student => student.Id, student => string.Join(" ",
            new[] { student.FirstName, student.MiddleName, student.LastName }.Where(name => !string.IsNullOrWhiteSpace(name))).Trim());
        var keys = new Dictionary<(Guid Exam, Guid Student), StudentExamPreviewKey>();
        var source = target.Select(mark => new StudentExamPreviewKey { ExamId = mark.ExamId, StudentId = mark.StudentId, DivisionId = mark.DivisionId });
        await foreach (var key in (IAsyncEnumerable<StudentExamPreviewKey>)source)
            if (examMap.ContainsKey(key.ExamId) && (!allBatch || studentNames.ContainsKey(key.StudentId))) keys.TryAdd((key.ExamId, key.StudentId), key);
        var selected = keys.Values.OrderBy(key => allBatch ? studentNames[key.StudentId] : string.Empty)
            .ThenBy(key => examMap[key.ExamId].ExamName).Skip(PreviewSkip(request)).Take(request.PageSize).ToList();
        payload.TotalCount = keys.Count; payload.Page = request.Page; payload.PageSize = request.PageSize;
        if (selected.Count == 0) return payload;
        var pageExamIds = selected.Select(key => key.ExamId).Distinct().ToList();
        var examData = await asyncExecuter.ToListAsync(exams.Where(exam => pageExamIds.Contains(exam.Id)).Select(exam => new
        {
            exam.Id, exam.ExamName, exam.StandardId,
            Total = exam.ExamSubjects.Sum(subject => subject.TotalMarks ?? 0), Passing = exam.ExamSubjects.Sum(subject => subject.PassingMarks ?? 0),
            Date = exam.ExamSubjects.Where(subject => subject.ExamDate.HasValue).OrderBy(subject => subject.ExamDate)
                .Select(subject => subject.ExamDate).FirstOrDefault()
        }));
        var examDataMap = examData.ToDictionary(exam => exam.Id);
        var parameter = Expression.Parameter(typeof(ExamMark), "mark");
        var predicate = Expression.Lambda<Func<ExamMark, bool>>(BuildStudentExamPreviewPredicate(selected, parameter, 0, selected.Count), parameter);
        var totals = await asyncExecuter.ToListAsync(target.Where(predicate).GroupBy(mark => new { mark.ExamId, mark.StudentId })
            .Select(group => new { group.Key.ExamId, group.Key.StudentId, Obtained = group.Sum(mark => mark.IsAbsent ? 0 : mark.MarksObtained ?? 0), IsAbsent = group.All(mark => mark.IsAbsent) }));
        var totalMap = totals.ToDictionary(total => (total.ExamId, total.StudentId));
        var participants = await asyncExecuter.ToListAsync(marks.Where(mark => pageExamIds.Contains(mark.ExamId)).GroupBy(mark => new { mark.ExamId, mark.StudentId })
            .Select(group => new { group.Key.ExamId, group.Key.StudentId, Obtained = group.Sum(mark => mark.IsAbsent ? 0 : mark.MarksObtained ?? 0) }));
        var allExamIds = metadata.Select(exam => exam.Id).ToList();
        var order = new Dictionary<(Guid Exam, Guid Student), long>();
        long ordinal = 0;
        var rankSource = marks.Where(mark => allExamIds.Contains(mark.ExamId)).Select(mark => new StudentExamPreviewKey { ExamId = mark.ExamId, StudentId = mark.StudentId });
        await foreach (var key in (IAsyncEnumerable<StudentExamPreviewKey>)rankSource)
        {
            if (pageExamIds.Contains(key.ExamId)) order.TryAdd((key.ExamId, key.StudentId), ordinal);
            ordinal++;
        }
        var ranks = participants.GroupBy(total => total.ExamId).ToDictionary(group => group.Key,
            group => group.OrderByDescending(total => total.Obtained).ThenBy(total => order[(total.ExamId, total.StudentId)])
                .Select((total, index) => (total.StudentId, Rank: index + 1)).ToDictionary(total => total.StudentId, total => total.Rank));
        var standardNames = new Dictionary<Guid, string>();
        var divisionNames = new Dictionary<Guid, string>();
        if (allBatch)
        {
            var standardIds = examData.Select(exam => exam.StandardId).Distinct().ToList();
            var divisionIds = selected.Select(key => key.DivisionId).Distinct().ToList();
            standardNames = (await asyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync()).Where(standard => standardIds.Contains(standard.Id))
                .Select(standard => new { standard.Id, standard.Name }))).ToDictionary(standard => standard.Id, standard => standard.Name);
            divisionNames = (await asyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync()).Where(division => divisionIds.Contains(division.Id))
                .Select(division => new { division.Id, division.Name }))).ToDictionary(division => division.Id, division => division.Name);
        }
        foreach (var key in selected)
        {
            var total = totalMap[(key.ExamId, key.StudentId)]; var exam = examDataMap[key.ExamId];
            var percentage = exam.Total > 0 ? Math.Round(total.Obtained / exam.Total * 100, 2) : 0;
            var row = new Dictionary<string, string?>
            {
                ["ExamName"] = exam.ExamName, ["TotalMarks"] = exam.Total.ToString("F0"),
                ["ObtainedMarks"] = total.IsAbsent ? "Absent" : total.Obtained.ToString("F2"),
                ["Percentage"] = total.IsAbsent ? "—" : $"{percentage}%", ["Grade"] = total.IsAbsent ? "—" : ComputeGrade(total.Obtained, exam.Total),
                ["Rank"] = total.IsAbsent ? "—" : ranks[key.ExamId][key.StudentId].ToString(),
                ["ResultStatus"] = total.IsAbsent ? "Absent" : total.Obtained >= exam.Passing ? "Pass" : "Fail"
            };
            if (allBatch)
            {
                row["StudentName"] = studentNames[key.StudentId]; row["ClassName"] = standardNames.GetValueOrDefault(exam.StandardId, "—");
                row["Division"] = divisionNames.GetValueOrDefault(key.DivisionId, "—");
            }
            else row["ExamDate"] = exam.Date?.ToString("yyyy-MM-dd") ?? "—";
            payload.Rows.Add(row);
        }
        return payload;
    }

    private static Expression BuildStudentExamPreviewPredicate(List<StudentExamPreviewKey> keys, ParameterExpression item, int start, int count)
    {
        if (count > 1)
        {
            var left = count / 2;
            return Expression.OrElse(BuildStudentExamPreviewPredicate(keys, item, start, left), BuildStudentExamPreviewPredicate(keys, item, start + left, count - left));
        }
        var key = keys[start];
        return Expression.AndAlso(Expression.Equal(Expression.Property(item, nameof(ExamMark.ExamId)), Expression.Constant(key.ExamId)),
            Expression.Equal(Expression.Property(item, nameof(ExamMark.StudentId)), Expression.Constant(key.StudentId)));
    }

    private sealed class StudentExamPreviewKey
    {
        public Guid ExamId { get; init; }
        public Guid StudentId { get; init; }
        public Guid DivisionId { get; init; }
    }
}
