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
    private AcademicReportExportRows CreateStudentExamExport(AcademicReportRequestDto request, int batchSize, CancellationToken cancellationToken)
    {
        var allBatch = request.ReportType == AcademicReportTypes.StudentAllBatchResult;
        var metadata = NewExamExportMetadata(request);
        (string Id, string Label)[] columns = allBatch
            ? [("StudentName", "Student Name"), ("ClassName", "Class"), ("Division", "Division"), ("ExamName", "Exam Name"),
                ("TotalMarks", "Total Marks"), ("ObtainedMarks", "Obtained Marks"), ("Percentage", "Percentage"), ("Grade", "Grade"), ("Rank", "Rank"), ("ResultStatus", "Result Status")]
            : [("ExamName", "Exam Name"), ("ExamDate", "Exam Date"), ("TotalMarks", "Total Marks"), ("ObtainedMarks", "Obtained Marks"),
                ("Percentage", "Percentage"), ("Grade", "Grade"), ("Rank", "Rank"), ("ResultStatus", "Result Status")];
        metadata.Columns = columns.Select(column => new AcademicReportColumnDto { Id = column.Id, Label = column.Label }).ToList();
        return new AcademicReportExportRows(metadata, ReadStudentExamExportAsync(request, batchSize, cancellationToken));
    }

    private async IAsyncEnumerable<Dictionary<string, string?>> ReadStudentExamExportAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var allBatch = request.ReportType == AcademicReportTypes.StudentAllBatchResult;
        var source = RestoreStudentExamSourceOrderAsync(request, batchSize, cancellationToken);
        await foreach (var row in AcademicReportExternalSort.SortAsync(source, (left, right) =>
        {
            var order = allBatch ? Comparer<string?>.Default.Compare(left.Values!.GetValueOrDefault("StudentName"), right.Values!.GetValueOrDefault("StudentName")) : 0;
            if (order == 0) order = Comparer<string?>.Default.Compare(left.Values!["ExamName"], right.Values!["ExamName"]);
            return order != 0 ? order : left.Ordinal.CompareTo(right.Ordinal);
        }, cancellationToken: cancellationToken)) yield return row.Values!;
    }

    private async IAsyncEnumerable<StudentExamExportRow> RestoreStudentExamSourceOrderAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Guid? examId = null, studentId = null;
        long ordinal = long.MaxValue;
        var source = ReadStudentExamOrderRecordsAsync(request, batchSize, cancellationToken);
        await foreach (var record in AcademicReportExternalSort.SortAsync(source, (a, b) =>
        {
            var order = a.ExamId.CompareTo(b.ExamId);
            if (order == 0) order = a.StudentId.CompareTo(b.StudentId);
            // Encounter records precede their generated row within each exam/student group.
            if (order == 0) order = (a.Values != null).CompareTo(b.Values != null);
            return order;
        }, cancellationToken: cancellationToken))
        {
            if (record.ExamId != examId || record.StudentId != studentId)
            { examId = record.ExamId; studentId = record.StudentId; ordinal = long.MaxValue; }
            if (record.Values == null) ordinal = Math.Min(ordinal, record.Ordinal);
            else yield return record with { Ordinal = ordinal };
        }
    }

    private async IAsyncEnumerable<StudentExamExportRow> ReadStudentExamOrderRecordsAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Filters.StudentIds is not { Count: > 0 }) yield break;
        using (examMarkRepository.DisableTracking())
        {
            var query = (await examMarkRepository.GetQueryableAsync()).Where(mark => request.Filters.StudentIds.Contains(mark.StudentId));
            if (request.Filters.DivisionIds is { Count: > 0 }) query = query.Where(mark => request.Filters.DivisionIds.Contains(mark.DivisionId));
            long ordinal = 0;
            // The legacy report has no secondary sort for equal exam names. Stream its exact source
            // query without introducing an ORDER BY; disk-backed records retain that encounter order.
            // No other database command runs until this reader has been disposed.
            await foreach (var mark in ((IAsyncEnumerable<ExamMark>)query).WithCancellation(cancellationToken))
                yield return new StudentExamExportRow(mark.ExamId, mark.StudentId, null, ordinal++);
        }
        await foreach (var row in BuildStudentExamExportRowsAsync(request, batchSize, cancellationToken)) yield return row;
    }

    private async IAsyncEnumerable<StudentExamExportRow> BuildStudentExamExportRowsAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var studentIds = request.Filters.StudentIds;
        if (studentIds is not { Count: > 0 }) yield break;
        using var examTracking = examRepository.DisableTracking();
        using var markTracking = examMarkRepository.DisableTracking();
        using var studentTracking = studentRepository.DisableTracking();
        var targetMarks = (await examMarkRepository.GetQueryableAsync()).Where(mark => studentIds.Contains(mark.StudentId));
        if (request.Filters.DivisionIds is { Count: > 0 }) targetMarks = targetMarks.Where(mark => request.Filters.DivisionIds.Contains(mark.DivisionId));
        var exams = await examRepository.WithDetailsAsync(exam => exam.ExamSubjects);
        exams = exams.Where(exam => targetMarks.Any(mark => mark.ExamId == exam.Id));
        if (request.Filters.ClassIds is { Count: > 0 }) exams = exams.Where(exam => request.Filters.ClassIds.Contains(exam.StandardId));
        var allBatch = request.ReportType == AcademicReportTypes.StudentAllBatchResult;
        var (standards, divisions, _, _, _, _) = await LoadLookupsAsync(includeStandards: allBatch, includeDivisions: allBatch);
        var studentQuery = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
        // One exam graph at a time; disk-backed participant totals preserve rank ties without retaining mark entities.
        for (var examOffset = 0; ; examOffset++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var exam = await asyncExecuter.FirstOrDefaultAsync(exams.OrderBy(exam => exam.Id).Skip(examOffset).Take(1), cancellationToken);
            if (exam == null) yield break;
            var max = exam.ExamSubjects.Sum(subject => subject.TotalMarks ?? 0);
            var passing = exam.ExamSubjects.Sum(subject => subject.PassingMarks ?? 0);
            var date = exam.ExamSubjects.Where(subject => subject.ExamDate.HasValue).OrderBy(subject => subject.ExamDate)
                .Select(subject => subject.ExamDate!.Value.ToString("yyyy-MM-dd")).FirstOrDefault() ?? "—";
            var allMarks = (await examMarkRepository.GetQueryableAsync()).Where(mark => mark.ExamId == exam.Id);
            await foreach (var ranked in ReadExamRankBatchesAsync(allMarks, batchSize, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var selected = ranked.Where(total => studentIds.Contains(total.StudentId)).Select(total => total.StudentId).ToList();
                if (selected.Count > 0)
                {
                    var marks = await asyncExecuter.ToListAsync(targetMarks.Where(mark => mark.ExamId == exam.Id && selected.Contains(mark.StudentId)), cancellationToken);
                    var students = allBatch
                        ? await asyncExecuter.ToListAsync(studentQuery.Where(student => selected.Contains(student.Id)), cancellationToken)
                        : [];
                    var studentMap = students.ToDictionary(student => student.Id);
                    var rankMap = ranked.ToDictionary(total => total.StudentId, total => total.Rank);
                    foreach (var group in marks.GroupBy(mark => mark.StudentId))
                    {
                        if (allBatch && !studentMap.ContainsKey(group.Key)) continue;
                        var absent = group.All(mark => mark.IsAbsent);
                        var obtained = group.Where(mark => !mark.IsAbsent).Sum(mark => mark.MarksObtained ?? 0);
                        var percentage = max > 0 ? Math.Round(obtained / max * 100, 2) : 0;
                        var row = new Dictionary<string, string?>
                        {
                            ["ExamName"] = exam.ExamName, ["TotalMarks"] = max.ToString("F0"),
                            ["ObtainedMarks"] = absent ? "Absent" : obtained.ToString("F2"), ["Percentage"] = absent ? "—" : $"{percentage}%",
                            ["Grade"] = absent ? "—" : ComputeGrade(obtained, max), ["Rank"] = absent ? "—" : rankMap[group.Key].ToString(),
                            ["ResultStatus"] = absent ? "Absent" : obtained >= passing ? "Pass" : "Fail"
                        };
                        if (allBatch)
                        {
                            row["StudentName"] = BuildStudentName(studentMap[group.Key]);
                            row["ClassName"] = standards.GetValueOrDefault(exam.StandardId, "—");
                            row["Division"] = divisions.GetValueOrDefault(group.First().DivisionId, "—");
                        }
                        else row["ExamDate"] = date;
                        yield return new StudentExamExportRow(exam.Id, group.Key, row, 0);
                    }
                }
            }
        }
    }

    private sealed record StudentExamExportRow(Guid ExamId, Guid StudentId, Dictionary<string, string?>? Values, long Ordinal);
}
