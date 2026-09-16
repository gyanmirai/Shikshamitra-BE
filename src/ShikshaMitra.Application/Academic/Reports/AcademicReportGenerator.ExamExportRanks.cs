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
    private async IAsyncEnumerable<ExamExportRankMark> ReadExamRankMarksAsync(IQueryable<ExamMark> marks,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long ordinal = 0;
        await foreach (var mark in ((IAsyncEnumerable<ExamMark>)marks).WithCancellation(cancellationToken))
            yield return new(mark.StudentId, mark.IsAbsent ? 0 : mark.MarksObtained ?? 0, ordinal++, mark.IsAbsent, mark.DivisionId);
    }

    private async IAsyncEnumerable<ExamExportRankMark> ReadExamRankTotalsAsync(IQueryable<ExamMark> marks,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ExamExportRankMark? total = null;
        // Retain source order inside each student's group, including the first division and sum order.
        await foreach (var mark in AcademicReportExternalSort.SortAsync(ReadExamRankMarksAsync(marks, cancellationToken),
            (a, b) => a.StudentId.CompareTo(b.StudentId), cancellationToken: cancellationToken))
        {
            if (total != null && total.StudentId != mark.StudentId)
            { yield return total; total = null; }
            total = total == null ? mark : total with { Obtained = total.Obtained + mark.Obtained, IsAbsent = total.IsAbsent && mark.IsAbsent };
        }
        if (total != null) yield return total;
    }

    private async IAsyncEnumerable<List<ExamExportRankedStudent>> ReadExamRankBatchesAsync(IQueryable<ExamMark> marks,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var batch = new List<ExamExportRankedStudent>(batchSize);
        var rank = 0;
        await foreach (var total in AcademicReportExternalSort.SortAsync(ReadExamRankTotalsAsync(marks, cancellationToken), (a, b) =>
        {
            var order = b.Obtained.CompareTo(a.Obtained);
            return order != 0 ? order : a.Ordinal.CompareTo(b.Ordinal);
        }, cancellationToken: cancellationToken))
        {
            batch.Add(new(total.StudentId, ++rank));
            if (batch.Count < batchSize) continue;
            yield return batch;
            batch = new(batchSize);
        }
        if (batch.Count > 0) yield return batch;
    }

    private sealed record ExamExportRankMark(Guid StudentId, double Obtained, long Ordinal, bool IsAbsent, Guid DivisionId);
    private sealed record ExamExportRankedStudent(Guid StudentId, int Rank);

    private async IAsyncEnumerable<ExamExportStudentTotal> ReadExamGradeRankTotalsAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var tracking = examMarkRepository.DisableTracking();
        var query = await GetExamExportMarksAsync(request.Filters.ExamId!.Value, request.Filters);
        var batch = new List<ExamExportRankMark>(batchSize);
        await foreach (var total in ReadExamRankTotalsAsync(query, cancellationToken))
        {
            batch.Add(total);
            if (batch.Count < batchSize) continue;
            foreach (var row in await LoadExamGradeRankStudentsAsync(batch, cancellationToken)) yield return row;
            batch.Clear();
        }
        if (batch.Count > 0)
            foreach (var row in await LoadExamGradeRankStudentsAsync(batch, cancellationToken)) yield return row;
    }

    private async Task<List<ExamExportStudentTotal>> LoadExamGradeRankStudentsAsync(List<ExamExportRankMark> batch, CancellationToken cancellationToken)
    {
        var ids = batch.Select(total => total.StudentId).ToList();
        var students = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
        var existing = (await asyncExecuter.ToListAsync(students.Where(student => ids.Contains(student.Id)).Select(student => student.Id), cancellationToken)).ToHashSet();
        return batch.Select(total => new ExamExportStudentTotal(total.StudentId, total.Obtained, total.IsAbsent, total.DivisionId,
            existing.Contains(total.StudentId), null, null, total.Ordinal)).ToList();
    }
}
