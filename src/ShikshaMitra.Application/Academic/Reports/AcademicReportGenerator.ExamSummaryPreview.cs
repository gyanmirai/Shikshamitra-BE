using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportGenerator
{
    private async Task<AcademicReportPreviewDto?> TryBuildExamSummaryPreviewAsync(AcademicReportRequestDto request, AcademicReportPreviewDto payload)
    {
        if (!IsExamSummaryExport(request.ReportType)) return null;
        using var examTracking = examRepository.DisableTracking();
        using var gradeTracking = gradingScaleRepository.DisableTracking();
        var query = await examRepository.WithDetailsAsync(exam => exam.ExamSubjects);
        var exam = request.Filters.ExamId.HasValue
            ? await asyncExecuter.FirstOrDefaultAsync(query.Where(exam => exam.Id == request.Filters.ExamId.Value)) : null;
        var data = exam == null ? new ExamReportData([], 0, 0, [], [], [], [], null)
            : await CreateExamExportMetadataAsync(exam, CancellationToken.None);
        if (request.ReportType is AcademicReportTypes.GenderWiseGradeAnalysis or AcademicReportTypes.ResultStatisticsReport)
            payload.Columns = ExamSummaryColumns(request.ReportType);
        else await FormatExamExportBatchAsync(request, payload, data);
        if (!request.Filters.ExamId.HasValue || (exam == null && request.ReportType != AcademicReportTypes.ResultStatisticsReport)) return payload;
        var marks = await GetExamExportMarksAsync(request.Filters.ExamId.Value, request.Filters);
        var originalMarks = (await examMarkRepository.GetQueryableAsync()).Where(mark => mark.ExamId == request.Filters.ExamId.Value);
        if (request.Filters.DivisionIds is { Count: > 0 }) originalMarks = originalMarks.Where(mark => request.Filters.DivisionIds.Contains(mark.DivisionId));
        var hasMarks = await asyncExecuter.AnyAsync(request.ReportType is AcademicReportTypes.GenderWiseGradeAnalysis or AcademicReportTypes.ResultStatisticsReport
            ? originalMarks : marks);
        if (!hasMarks) return payload;
        var rows = new List<Dictionary<string, string?>>();
        var source = request.ReportType == AcademicReportTypes.StudentGradeAnalysisSubjectWise
            ? ReadExamGradePreviewRowsAsync(request, data, marks)
            : ReadExamSummaryExportAsync(request, exam, data, marks, 256, CancellationToken.None,
                request.ReportType == AcademicReportTypes.ResultListOverallGradeAnalysis
                    ? ReadExamPreviewRankTotalsAsync(marks) : null);
        await foreach (var row in source) rows.Add(row);
        payload.TotalCount = rows.Count;
        if (request.ReportType is AcademicReportTypes.ExamSummaryReport or AcademicReportTypes.ResultListPassFailOverallAnalysis)
        {
            payload.Page = 1; payload.PageSize = 1; payload.Rows = rows;
        }
        else
        {
            payload.Page = request.Page; payload.PageSize = request.PageSize;
            payload.Rows = rows.Skip(PreviewSkip(request)).Take(request.PageSize).ToList();
        }
        return payload;
    }

    private async IAsyncEnumerable<ExamExportStudentTotal> ReadExamPreviewRankTotalsAsync(IQueryable<ExamMark> marks)
    {
        var order = (await asyncExecuter.ToListAsync(marks.Select(mark => mark.StudentId))).Distinct()
            .Select((id, index) => (Id: id, Index: index)).ToDictionary(item => item.Id, item => item.Index);
        var totals = new List<ExamExportStudentTotal>();
        await foreach (var total in ReadExamStudentTotalsAsync(marks, 256, CancellationToken.None)) totals.Add(total);
        foreach (var total in totals.OrderByDescending(total => total.Obtained).ThenBy(total => order[total.StudentId])) yield return total;
    }

    private async IAsyncEnumerable<Dictionary<string, string?>> ReadExamGradePreviewRowsAsync(AcademicReportRequestDto request,
        ExamReportData metadata, IQueryable<ExamMark> marks, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var maximum = metadata.ExamSubjects.ToDictionary(subject => subject.SubjectId, subject => subject.TotalMarks ?? 0);
        var firstMaximum = new Dictionary<string, double>();
        // The grade's displayed maximum belongs to its first source subject, even when later subjects have higher maxima.
        var source = marks.Select(mark => new ExamGradePreviewSource
        { SubjectId = mark.SubjectId, MarksObtained = mark.MarksObtained, IsAbsent = mark.IsAbsent });
        await foreach (var mark in ((IAsyncEnumerable<ExamGradePreviewSource>)source).WithCancellation(cancellationToken))
        {
            if (mark.IsAbsent || (request.Filters.SubjectIds is { Count: > 0 } && !request.Filters.SubjectIds.Contains(mark.SubjectId))) continue;
            var max = maximum.GetValueOrDefault(mark.SubjectId);
            var grade = ComputeGrade(mark.MarksObtained ?? 0, max, metadata.DefaultGradeRanges);
            firstMaximum.TryAdd(grade, max);
        }
        var selected = marks.Where(mark => !mark.IsAbsent);
        if (request.Filters.SubjectIds is { Count: > 0 }) selected = selected.Where(mark => request.Filters.SubjectIds.Contains(mark.SubjectId));
        var buckets = await asyncExecuter.ToListAsync(selected.GroupBy(mark => new { mark.SubjectId, mark.MarksObtained })
            .Select(group => new { group.Key.SubjectId, Marks = group.Key.MarksObtained ?? 0, Count = group.Count() }), cancellationToken);
        var groups = new Dictionary<string, ExamExportStatistics>();
        var total = 0;
        foreach (var bucket in buckets)
        {
            var grade = ComputeGrade(bucket.Marks, maximum.GetValueOrDefault(bucket.SubjectId), metadata.DefaultGradeRanges);
            if (!groups.TryGetValue(grade, out var stats)) groups[grade] = stats = new ExamExportStatistics { TotalMax = firstMaximum[grade] };
            stats.Add(bucket.Marks, false, false, bucket.Count); total += bucket.Count;
        }
        foreach (var grade in ExamExportGrades.Take(8))
        {
            if (!groups.TryGetValue(grade, out var stats)) continue;
            yield return new()
            {
                ["Grade"] = grade, ["StudentCount"] = stats.Count.ToString(), ["Percentage"] = Percent(stats.Count, total),
                ["TotalMarks"] = stats.TotalMax.ToString("F0"), ["HighestMarks"] = stats.Highest.ToString("F2"), ["LowestMarks"] = stats.Lowest.ToString("F2")
            };
        }
    }

    private sealed class ExamGradePreviewSource
    {
        public Guid SubjectId { get; init; }
        public double? MarksObtained { get; init; }
        public bool IsAbsent { get; init; }
    }
}
