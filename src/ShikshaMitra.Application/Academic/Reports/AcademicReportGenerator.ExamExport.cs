using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Students;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportGenerator
{
    private async Task<AcademicReportExportRows?> TryCreateExamExportAsync(AcademicReportRequestDto request,
        Guid userId, Guid? tenantId, CancellationToken cancellationToken, int batchSize)
    {
        if (request.ReportType is AcademicReportTypes.StudentAllBatchResult or AcademicReportTypes.StudentAllExamResult)
            return CreateStudentExamExport(request, batchSize, cancellationToken);
        if (!IsExamDetailExport(request.ReportType) && !IsExamSummaryExport(request.ReportType)) return null;
        using var examTracking = examRepository.DisableTracking();
        using var subjectTracking = subjectRepository.DisableTracking();
        using var gradeTracking = gradingScaleRepository.DisableTracking();
        var examQuery = await examRepository.WithDetailsAsync(exam => exam.ExamSubjects);
        var exam = request.Filters.ExamId.HasValue
            ? await asyncExecuter.FirstOrDefaultAsync(examQuery.Where(exam => exam.Id == request.Filters.ExamId.Value), cancellationToken)
            : null;
        var empty = new ExamReportData([], 0, 0, [], [], [], [], null);
        var data = exam == null ? empty : await CreateExamExportMetadataAsync(exam, cancellationToken);
        var metadata = NewExamExportMetadata(request);
        if (request.ReportType is AcademicReportTypes.GenderWiseGradeAnalysis or AcademicReportTypes.ResultStatisticsReport)
            metadata.Columns = ExamSummaryColumns(request.ReportType);
        else await FormatExamExportBatchAsync(request, metadata, data);
        if (!request.Filters.ExamId.HasValue || (exam == null && request.ReportType != AcademicReportTypes.ResultStatisticsReport))
            return AcademicReportExportRows.FromReport(metadata);

        var marksQuery = await GetExamExportMarksAsync(request.Filters.ExamId.Value, request.Filters);
        if (IsExamSummaryExport(request.ReportType))
            return new AcademicReportExportRows(metadata, ReadExamSummaryExportAsync(request, exam, data, marksQuery, batchSize, cancellationToken));
        if (request.ReportType is AcademicReportTypes.DetailedResultSheet or AcademicReportTypes.ResultWithAllSubjectList &&
            await asyncExecuter.AnyAsync(marksQuery, cancellationToken))
        {
            metadata.Columns = [new() { Id = "RollNo", Label = "Roll No" }, new() { Id = "StudentName", Label = "Student Name" }];
            metadata.Columns.AddRange(data.ExamSubjects.Select(subject => new { subject.SubjectId, Name = data.SubjectNames.GetValueOrDefault(subject.SubjectId, "—") })
                .OrderBy(subject => subject.Name).Select(subject => new AcademicReportColumnDto { Id = $"subj_{subject.SubjectId}", Label = subject.Name }));
            metadata.Columns.AddRange(new[] { "Total", "Percentage", "Grade", "Status" }.Select(name => new AcademicReportColumnDto { Id = name, Label = name }));
        }
        return new AcademicReportExportRows(metadata, ReadExamDetailExportAsync(request, exam!, data, marksQuery, batchSize, cancellationToken));
    }

    private static AcademicReportPreviewDto NewExamExportMetadata(AcademicReportRequestDto request) => new()
    {
        ReportType = request.ReportType, GeneratedAtUtc = DateTime.UtcNow,
        FilterSummary = BuildFilterSummary(request.Filters), NormalizedFilters = request.Filters, Page = 1
    };

    private async Task<ExamReportData> CreateExamExportMetadataAsync(Exam exam, CancellationToken cancellationToken)
    {
        var subjects = exam.ExamSubjects.ToList();
        var ids = subjects.Select(subject => subject.SubjectId).ToList();
        var query = await subjectRepository.GetQueryableAsync();
        var names = await asyncExecuter.ToListAsync(query.Where(subject => ids.Contains(subject.Id))
            .Select(subject => new { subject.Id, subject.Name }), cancellationToken);
        IReadOnlyCollection<GradeRange>? ranges = null;
        if (exam.GradingScaleId.HasValue)
        {
            var scales = await gradingScaleRepository.WithDetailsAsync(scale => scale.GradeRanges);
            var scale = await asyncExecuter.FirstOrDefaultAsync(scales.Where(scale => scale.Id == exam.GradingScaleId.Value), cancellationToken);
            ranges = scale?.GradeRanges.ToList();
        }
        return new ExamReportData(subjects, subjects.Sum(subject => subject.TotalMarks ?? 0), subjects.Sum(subject => subject.PassingMarks ?? 0),
            [], [], names.ToDictionary(subject => subject.Id, subject => subject.Name), [], ranges);
    }

    private async Task<IQueryable<ExamMark>> GetExamExportMarksAsync(Guid examId, AcademicReportFilterDto filters)
    {
        var marks = (await examMarkRepository.GetQueryableAsync()).Where(mark => mark.ExamId == examId);
        if (filters.DivisionIds is { Count: > 0 }) marks = marks.Where(mark => filters.DivisionIds.Contains(mark.DivisionId));
        if (filters.ClassIds is { Count: > 0 })
        {
            var students = await studentRepository.GetQueryableAsync();
            marks = marks.Where(mark => students.Any(student => student.Id == mark.StudentId && student.StandardId.HasValue && filters.ClassIds.Contains(student.StandardId.Value)));
        }
        return marks;
    }

    private async IAsyncEnumerable<Dictionary<string, string?>> ReadExamDetailExportAsync(AcademicReportRequestDto request,
        Exam exam, ExamReportData metadata, IQueryable<ExamMark> marks, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rank = 0;
        var subjectRanks = new Dictionary<Guid, int>();
        var subjectOrder = new Dictionary<Guid, long>();
        var rows = BuildExamDetailExportRowsAsync(request, exam, metadata, batchSize, subjectOrder, cancellationToken);
        if (request.ReportType == AcademicReportTypes.BrightStudentListSubjectWise)
        {
            // Finish collecting subject group encounter order before any comparison uses it.
            rows = AcademicReportExternalSort.SortAsync(rows, (left, right) => left.Ordinal.CompareTo(right.Ordinal), cancellationToken: cancellationToken);
        }
        await foreach (var row in AcademicReportExternalSort.SortAsync(rows,
            (left, right) =>
            {
                var comparison = CompareExamExportRows(left, right, request.ReportType, subjectOrder);
                return comparison != 0 ? comparison : left.Ordinal.CompareTo(right.Ordinal);
            }, cancellationToken: cancellationToken))
        {
            if (request.ReportType is AcademicReportTypes.GradewiseResultList or AcademicReportTypes.BrightStudentAnalysis)
                row.Values["Rank"] = (++rank).ToString();
            if (request.ReportType is AcademicReportTypes.BrightStudentListSubjectWise or AcademicReportTypes.ResultListWithSubjectGrade)
            {
                var subjectId = row.SubjectId!.Value;
                subjectRanks[subjectId] = subjectRanks.GetValueOrDefault(subjectId) + 1;
                row.Values["Rank"] = subjectRanks[subjectId].ToString();
            }
            yield return row.Values;
        }
    }

    private async IAsyncEnumerable<ExamExportRow> BuildExamDetailExportRowsAsync(AcademicReportRequestDto request,
        Exam exam, ExamReportData metadata, int batchSize, Dictionary<Guid, long> subjectOrder, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var pivot = request.ReportType is AcademicReportTypes.DetailedResultSheet or AcademicReportTypes.ResultWithAllSubjectList;
        var perMark = request.ReportType is AcademicReportTypes.SubjectwiseMarksReport or AcademicReportTypes.SubjectWiseGradeList
            or AcademicReportTypes.ResultListWithSubject or AcademicReportTypes.ResultListWithSubjectFailedDetails
            or AcademicReportTypes.ResultListWithSubjectGrade or AcademicReportTypes.ResultListWithSubjectWithGrace
            or AcademicReportTypes.BrightStudentListSubjectWise or AcademicReportTypes.WeakStudentAnalysisSubjectWise;
        await foreach (var sourceBatch in ReadExamExportBatchesAsync(exam, metadata, request.Filters, pivot, batchSize, cancellationToken))
        {
            var batch = sourceBatch.Data;
            foreach (var group in batch.Marks.GroupBy(mark => mark.StudentId))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!batch.StudentMap.TryGetValue(group.Key, out var student)) continue;
                var studentData = batch with { Marks = group.ToList(), StudentMap = new() { [student.Id] = student } };
                var studentOrdinal = sourceBatch.Ordinals[group.First().Id];
                var studentTotal = group.Where(mark => !mark.IsAbsent).Sum(mark => mark.MarksObtained ?? 0);
                var subjectGroups = perMark ? group.Select(mark => ((Guid?)mark.SubjectId, Marks: new List<ExamMark> { mark }))
                    : new[] { ((Guid?)null, studentData.Marks) };
                foreach (var (subjectId, selectedMarks) in subjectGroups)
                {
                    var ordinal = perMark ? sourceBatch.Ordinals[selectedMarks[0].Id] : studentOrdinal;
                    if (request.ReportType == AcademicReportTypes.BrightStudentListSubjectWise && !selectedMarks[0].IsAbsent &&
                        (request.Filters.SubjectIds is not { Count: > 0 } || request.Filters.SubjectIds.Contains(subjectId!.Value)))
                        subjectOrder[subjectId!.Value] = Math.Min(subjectOrder.GetValueOrDefault(subjectId.Value, long.MaxValue), ordinal);
                    var payload = await FormatExamExportBatchAsync(request, NewExamExportMetadata(request), studentData with { Marks = selectedMarks });
                    var obtained = selectedMarks.Where(mark => !mark.IsAbsent).Sum(mark => mark.MarksObtained ?? 0);
                    foreach (var values in payload.Rows)
                    {
                        if (request.ReportType == AcademicReportTypes.SubjectwiseMarksReport) values["TotalMarks"] = studentTotal.ToString("F2");
                        var sortSubject = values.GetValueOrDefault("Subject") ?? values.GetValueOrDefault("FailedSubject");
                        yield return new ExamExportRow(values, student.RollNo, obtained, subjectId, sortSubject == "—" ? string.Empty : sortSubject, ordinal, student.Id);
                    }
                }
            }
        }
    }

    private Task<AcademicReportPreviewDto> FormatExamExportBatchAsync(AcademicReportRequestDto request, AcademicReportPreviewDto metadata, ExamReportData data) => request.ReportType switch
    {
        AcademicReportTypes.ResultList => BuildResultListAsync(request, metadata, true, data),
        AcademicReportTypes.GradewiseResultList => BuildGradewiseResultListAsync(request, metadata, true, data),
        AcademicReportTypes.MarksRangeResultList => BuildMarksRangeResultListAsync(request, metadata, true, data),
        AcademicReportTypes.SubjectwiseMarksReport => BuildSubjectwiseMarksReportAsync(request, metadata, true, data),
        AcademicReportTypes.DetailedResultSheet or AcademicReportTypes.ResultWithAllSubjectList => BuildDetailedResultSheetAsync(request, metadata, true, data),
        AcademicReportTypes.SubjectWiseGradeList => BuildSubjectWiseGradeListAsync(request, metadata, true, data),
        AcademicReportTypes.ResultListWithSubject => BuildResultListWithSubjectAsync(request, metadata, true, data),
        AcademicReportTypes.ResultListWithSubjectFailedDetails => BuildResultListWithSubjectFailedDetailsAsync(request, metadata, true, data),
        AcademicReportTypes.ResultListWithSubjectGrade => BuildResultListWithSubjectGradeAsync(request, metadata, true, data),
        AcademicReportTypes.ResultListWithSubjectSmall => BuildResultListWithSubjectSmallAsync(request, metadata, true, data),
        AcademicReportTypes.ResultListWithSubjectWithGrace => BuildResultListWithSubjectWithGraceAsync(request, metadata, true, data),
        AcademicReportTypes.BrightStudentAnalysis => BuildBrightStudentAnalysisAsync(request, metadata, true, data),
        AcademicReportTypes.BrightStudentListSubjectWise => BuildBrightStudentListSubjectWiseAsync(request, metadata, true, data),
        AcademicReportTypes.WeakStudentAnalysis => BuildWeakStudentAnalysisAsync(request, metadata, true, data),
        AcademicReportTypes.WeakStudentAnalysisSubjectWise => BuildWeakStudentAnalysisSubjectWiseAsync(request, metadata, true, data),
        AcademicReportTypes.ExamSummaryReport => BuildExamSummaryReportAsync(request, metadata, true, data),
        AcademicReportTypes.AverageSubjectMarkAnalysis => BuildAverageSubjectMarkAnalysisAsync(request, metadata, true, data),
        AcademicReportTypes.SubjectAverageClassMarkAnalysis => BuildSubjectAverageClassMarkAnalysisAsync(request, metadata, true, data),
        AcademicReportTypes.StudentGradeAnalysisSubjectWise => BuildStudentGradeAnalysisSubjectWiseAsync(request, metadata, true, data),
        AcademicReportTypes.ResultListPassFailAnalysisSubjectWise => BuildResultListPassFailAnalysisSubjectWiseAsync(request, metadata, true, data),
        AcademicReportTypes.ResultListSubjectGradeAnalysis => BuildResultListSubjectGradeAnalysisAsync(request, metadata, true, data),
        AcademicReportTypes.StudentGradeAnalysis => BuildStudentGradeAnalysisAsync(request, metadata, true, data),
        AcademicReportTypes.ResultListPassFailOverallAnalysis => BuildResultListPassFailOverallAnalysisAsync(request, metadata, true, data),
        AcademicReportTypes.ResultListOverallGradeAnalysis => BuildResultListOverallGradeAnalysisAsync(request, metadata, true, data),
        _ => throw new InvalidOperationException("Unsupported exam row formatter.")
    };

    private static bool IsExamDetailExport(string type) => type is AcademicReportTypes.ResultList or AcademicReportTypes.GradewiseResultList
        or AcademicReportTypes.MarksRangeResultList or AcademicReportTypes.SubjectwiseMarksReport or AcademicReportTypes.DetailedResultSheet
        or AcademicReportTypes.ResultWithAllSubjectList or AcademicReportTypes.SubjectWiseGradeList or AcademicReportTypes.ResultListWithSubject
        or AcademicReportTypes.ResultListWithSubjectFailedDetails or AcademicReportTypes.ResultListWithSubjectGrade
        or AcademicReportTypes.ResultListWithSubjectSmall or AcademicReportTypes.ResultListWithSubjectWithGrace
        or AcademicReportTypes.BrightStudentAnalysis or AcademicReportTypes.BrightStudentListSubjectWise
        or AcademicReportTypes.WeakStudentAnalysis or AcademicReportTypes.WeakStudentAnalysisSubjectWise;

    internal static bool IsExamScopedReport(string type) =>
        IsExamDetailExport(type) || IsExamSummaryExport(type);

    private static int CompareExamExportRows(ExamExportRow left, ExamExportRow right, string type, IReadOnlyDictionary<Guid, long> subjectOrder)
    {
        var strings = Comparer<string?>.Default;
        int comparison;
        switch (type)
        {
            case AcademicReportTypes.ResultList:
                return strings.Compare(left.Values["RollNo"], right.Values["RollNo"]);
            case AcademicReportTypes.GradewiseResultList:
            case AcademicReportTypes.BrightStudentAnalysis:
                return ExamExportPercentage(right.Values).CompareTo(ExamExportPercentage(left.Values));
            case AcademicReportTypes.MarksRangeResultList:
                return right.Obtained.CompareTo(left.Obtained);
            case AcademicReportTypes.WeakStudentAnalysis:
                return ExamExportPercentage(left.Values).CompareTo(ExamExportPercentage(right.Values));
            case AcademicReportTypes.DetailedResultSheet:
            case AcademicReportTypes.ResultWithAllSubjectList:
                comparison = Nullable.Compare(left.RollNo, right.RollNo);
                return comparison != 0 ? comparison : left.StudentId.CompareTo(right.StudentId);
            case AcademicReportTypes.SubjectwiseMarksReport:
            case AcademicReportTypes.ResultListWithSubject:
                comparison = Nullable.Compare(left.RollNo, right.RollNo);
                return comparison != 0 ? comparison : strings.Compare(left.SubjectSortName, right.SubjectSortName);
            case AcademicReportTypes.BrightStudentListSubjectWise:
                comparison = strings.Compare(left.SubjectSortName, right.SubjectSortName);
                if (comparison == 0) comparison = subjectOrder[left.SubjectId!.Value].CompareTo(subjectOrder[right.SubjectId!.Value]);
                return comparison != 0 ? comparison : right.Obtained.CompareTo(left.Obtained);
            case AcademicReportTypes.ResultListWithSubjectGrade:
                comparison = strings.Compare(left.SubjectSortName, right.SubjectSortName);
                return comparison != 0 ? comparison : right.Obtained.CompareTo(left.Obtained);
            default:
                comparison = strings.Compare(left.Values["StudentName"], right.Values["StudentName"]);
                return comparison != 0 ? comparison : strings.Compare(left.SubjectSortName, right.SubjectSortName);
        }
    }

    private static double ExamExportPercentage(Dictionary<string, string?> values) =>
        double.TryParse(values.GetValueOrDefault("Percentage")?.TrimEnd('%'), NumberStyles.Number, CultureInfo.CurrentCulture, out var value) ? value : 0;

    private sealed record ExamExportRow(Dictionary<string, string?> Values, int? RollNo, double Obtained, Guid? SubjectId, string? SubjectSortName, long Ordinal, Guid StudentId);
}
