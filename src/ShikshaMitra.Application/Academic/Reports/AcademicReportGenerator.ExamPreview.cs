using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportGenerator
{
    private async Task<AcademicReportPreviewDto?> TryBuildExamPreviewAsync(AcademicReportRequestDto request, AcademicReportPreviewDto payload)
    {
        if (request.ReportType is not (AcademicReportTypes.ResultList or AcademicReportTypes.GradewiseResultList
            or AcademicReportTypes.MarksRangeResultList or AcademicReportTypes.BrightStudentAnalysis or AcademicReportTypes.WeakStudentAnalysis
            or AcademicReportTypes.ResultListWithSubjectSmall or AcademicReportTypes.DetailedResultSheet or AcademicReportTypes.ResultWithAllSubjectList)) return null;
        using var examTracking = examRepository.DisableTracking();
        using var gradeTracking = gradingScaleRepository.DisableTracking();
        var query = await examRepository.WithDetailsAsync(exam => exam.ExamSubjects);
        var exam = request.Filters.ExamId.HasValue
            ? await asyncExecuter.FirstOrDefaultAsync(query.Where(exam => exam.Id == request.Filters.ExamId.Value)) : null;
        var metadata = exam == null ? new ExamReportData([], 0, 0, [], [], [], [], null)
            : await CreateExamExportMetadataAsync(exam, CancellationToken.None);
        await FormatExamExportBatchAsync(request, payload, metadata);
        if (exam == null) return payload;
        var marks = await GetExamExportMarksAsync(exam.Id, request.Filters);
        if (!await asyncExecuter.AnyAsync(marks)) return payload;
        // Preserve first-student encounter order for equal grade and mark sort keys.
        var sourceOrder = (await asyncExecuter.ToListAsync(marks.Select(mark => mark.StudentId))).Distinct()
            .Select((id, index) => (Id: id, Ordinal: index)).ToDictionary(item => item.Id, item => item.Ordinal);
        var subjects = query.Where(item => item.Id == exam.Id).SelectMany(item => item.ExamSubjects);
        var needsFailed = request.ReportType == AcademicReportTypes.WeakStudentAnalysis;
        var totals = marks.GroupBy(mark => mark.StudentId).Select(group => new
        {
            StudentId = group.Key, Obtained = group.Sum(mark => mark.IsAbsent ? 0 : mark.MarksObtained ?? 0),
            IsAbsent = group.All(mark => mark.IsAbsent),
            Failed = needsFailed ? group.Count(mark => !mark.IsAbsent && (mark.MarksObtained ?? 0) <
                (subjects.Where(subject => subject.SubjectId == mark.SubjectId).Select(subject => subject.PassingMarks).FirstOrDefault() ?? 0)) : 0
        });
        var students = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
        var facts = await asyncExecuter.ToListAsync(from total in totals
            join student in students on total.StudentId equals student.Id
            select new
            {
                student.Id, student.FirstName, student.MiddleName, student.LastName, student.RollNo,
                total.Obtained, total.IsAbsent, total.Failed
            });
        var candidates = facts.Select(fact => new ExamPreviewStudentKey
        {
            Id = fact.Id, Name = string.Join(" ", new[] { fact.FirstName, fact.MiddleName, fact.LastName }.Where(name => !string.IsNullOrWhiteSpace(name))).Trim(),
            RollNo = fact.RollNo, Obtained = fact.Obtained, IsAbsent = fact.IsAbsent, Failed = fact.Failed,
            Percentage = metadata.TotalMaxMarks > 0 ? Math.Round(fact.Obtained / metadata.TotalMaxMarks * 100, 2) : 0,
            Grade = fact.IsAbsent ? "—" : ComputeGrade(fact.Obtained, metadata.TotalMaxMarks, metadata.DefaultGradeRanges), Ordinal = sourceOrder[fact.Id]
        }).OrderBy(item => item.Ordinal).ToList();
        IEnumerable<ExamPreviewStudentKey> filtered = candidates;
        switch (request.ReportType)
        {
            case AcademicReportTypes.GradewiseResultList:
                filtered = filtered.Where(item => string.IsNullOrWhiteSpace(request.Filters.Grade) || item.Grade.Equals(request.Filters.Grade, StringComparison.OrdinalIgnoreCase));
                break;
            case AcademicReportTypes.MarksRangeResultList:
                filtered = filtered.Where(item => !item.IsAbsent && item.Obtained >= (request.Filters.MinMarks ?? 0) && item.Obtained <= (request.Filters.MaxMarks ?? double.MaxValue));
                break;
            case AcademicReportTypes.BrightStudentAnalysis:
                filtered = filtered.Where(item => !item.IsAbsent && (string.IsNullOrWhiteSpace(request.Filters.Grade) || item.Grade.Equals(request.Filters.Grade, StringComparison.OrdinalIgnoreCase)));
                break;
            case AcademicReportTypes.WeakStudentAnalysis:
                filtered = filtered.Where(item => item.IsAbsent || item.Failed > 0);
                break;
        }
        var ordered = request.ReportType switch
        {
            AcademicReportTypes.ResultList => filtered.OrderBy(item => item.RollNo?.ToString() ?? "—"),
            AcademicReportTypes.GradewiseResultList or AcademicReportTypes.BrightStudentAnalysis => filtered.OrderByDescending(item => item.Percentage),
            AcademicReportTypes.MarksRangeResultList => filtered.OrderByDescending(item => item.Obtained),
            AcademicReportTypes.WeakStudentAnalysis => filtered.OrderBy(item => item.Percentage),
            AcademicReportTypes.DetailedResultSheet or AcademicReportTypes.ResultWithAllSubjectList => filtered.OrderBy(item => item.RollNo).ThenBy(item => item.Id),
            _ => filtered.OrderBy(item => item.Name)
        };
        var keys = ordered.ToList();
        var skip = PreviewSkip(request);
        var selected = keys.Skip(skip).Take(request.PageSize).ToList();
        payload.Page = request.Page; payload.PageSize = request.PageSize; payload.TotalCount = keys.Count;
        if (selected.Count == 0)
        {
            if (request.ReportType is AcademicReportTypes.DetailedResultSheet or AcademicReportTypes.ResultWithAllSubjectList)
            {
                payload.Columns = [new() { Id = "RollNo", Label = "Roll No" }, new() { Id = "StudentName", Label = "Student Name" }];
                payload.Columns.AddRange(metadata.ExamSubjects.Select(subject => new { subject.SubjectId, Name = metadata.SubjectNames.GetValueOrDefault(subject.SubjectId, "—") })
                    .OrderBy(subject => subject.Name).Select(subject => new AcademicReportColumnDto { Id = $"subj_{subject.SubjectId}", Label = subject.Name }));
                payload.Columns.AddRange(new[] { "Total", "Percentage", "Grade", "Status" }.Select(name => new AcademicReportColumnDto { Id = name, Label = name }));
            }
            return payload;
        }
        var ids = selected.Select(item => item.Id).ToList();
        var records = await asyncExecuter.ToListAsync(marks.Where(mark => ids.Contains(mark.StudentId)).Select(mark => new ExamPreviewMark
        {
            Id = mark.Id, StudentId = mark.StudentId, SubjectId = mark.SubjectId, DivisionId = mark.DivisionId,
            Obtained = mark.MarksObtained, IsAbsent = mark.IsAbsent, Feedback = mark.Feedback, TenantId = mark.TenantId
        }));
        var exportRecords = records.Select((mark, ordinal) => new ExamExportMarkRecord(mark.Id, mark.StudentId, mark.SubjectId, mark.DivisionId,
            mark.Obtained, mark.IsAbsent, mark.Feedback, mark.TenantId, ordinal)).ToList();
        var pivot = request.ReportType is AcademicReportTypes.DetailedResultSheet or AcademicReportTypes.ResultWithAllSubjectList;
        var batch = await LoadExamExportBatchAsync(exam, metadata, request.Filters, pivot, exportRecords, CancellationToken.None);
        var selectedStudents = selected.Where(item => batch.Data.StudentMap.ContainsKey(item.Id))
            .ToDictionary(item => item.Id, item => batch.Data.StudentMap[item.Id]);
        var pageData = batch.Data with
        {
            StudentMap = selectedStudents,
            Marks = batch.Data.Marks.OrderBy(mark => ids.IndexOf(mark.StudentId)).ToList()
        };
        var result = await FormatExamExportBatchAsync(request, payload, pageData);
        result.TotalCount = keys.Count; result.Page = request.Page; result.PageSize = request.PageSize;
        if (request.ReportType is AcademicReportTypes.GradewiseResultList or AcademicReportTypes.BrightStudentAnalysis)
            for (var index = 0; index < result.Rows.Count; index++) result.Rows[index]["Rank"] = (skip + index + 1).ToString();
        return result;
    }

    private sealed class ExamPreviewStudentKey
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int? RollNo { get; init; }
        public double Obtained { get; init; }
        public double Percentage { get; init; }
        public bool IsAbsent { get; init; }
        public int Failed { get; init; }
        public string Grade { get; init; } = string.Empty;
        public int Ordinal { get; init; }
    }

    private sealed class ExamPreviewMark
    {
        public Guid Id { get; init; }
        public Guid StudentId { get; init; }
        public Guid SubjectId { get; init; }
        public Guid DivisionId { get; init; }
        public double? Obtained { get; init; }
        public bool IsAbsent { get; init; }
        public string? Feedback { get; init; }
        public Guid? TenantId { get; init; }
    }
}
