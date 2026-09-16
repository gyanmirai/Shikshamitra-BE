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
    private async IAsyncEnumerable<ExamExportMarkRecord> ReadExamExportSourceMarksAsync(Guid examId,
        AcademicReportFilterDto filters, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var tracking = examMarkRepository.DisableTracking();
        // Preserve the original root query and its encounter order. Class filtering occurs after
        // the bounded student lookup, as in the preview, so it cannot change the mark query plan.
        var source = (await examMarkRepository.GetQueryableAsync()).Where(mark => mark.ExamId == examId);
        if (filters.DivisionIds is { Count: > 0 }) source = source.Where(mark => filters.DivisionIds.Contains(mark.DivisionId));
        long ordinal = 0;
        await foreach (var mark in ((IAsyncEnumerable<ExamMark>)source).WithCancellation(cancellationToken))
            yield return new(mark.Id, mark.StudentId, mark.SubjectId, mark.DivisionId, mark.MarksObtained,
                mark.IsAbsent, mark.Feedback, mark.TenantId, ordinal++);
    }

    private async IAsyncEnumerable<ExamExportBatch> ReadExamExportBatchesAsync(Exam exam, ExamReportData metadata,
        AcademicReportFilterDto filters, bool resolveApplicability, int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var batch = new List<ExamExportMarkRecord>();
        Guid? previousStudent = null;
        var studentCount = 0;
        // The disk sorter closes the database cursor before the first student lookup. Stable
        // grouping retains the original per-student mark order without retaining all marks.
        await foreach (var mark in AcademicReportExternalSort.SortAsync(ReadExamExportSourceMarksAsync(exam.Id, filters, cancellationToken),
            (left, right) => left.StudentId.CompareTo(right.StudentId), cancellationToken: cancellationToken))
        {
            if (previousStudent != mark.StudentId)
            {
                if (studentCount == batchSize)
                {
                    yield return await LoadExamExportBatchAsync(exam, metadata, filters, resolveApplicability, batch, cancellationToken);
                    batch.Clear();
                    studentCount = 0;
                }
                studentCount++;
                previousStudent = mark.StudentId;
            }
            batch.Add(mark);
        }
        if (batch.Count > 0)
            yield return await LoadExamExportBatchAsync(exam, metadata, filters, resolveApplicability, batch, cancellationToken);
    }

    private async Task<ExamExportBatch> LoadExamExportBatchAsync(Exam exam, ExamReportData metadata,
        AcademicReportFilterDto filters, bool resolveApplicability, List<ExamExportMarkRecord> records, CancellationToken cancellationToken)
    {
        using var tracking = studentRepository.DisableTracking();
        var ids = records.Select(mark => mark.StudentId).Distinct().ToList();
        var studentQuery = resolveApplicability
            ? await studentRepository.GetAcademicReportQueryableAsync()
            : await studentRepository.GetQueryableWithoutAutoIncludesAsync();
        studentQuery = studentQuery.Where(student => ids.Contains(student.Id));
        if (filters.ClassIds is { Count: > 0 })
            studentQuery = studentQuery.Where(student => student.StandardId.HasValue && filters.ClassIds.Contains(student.StandardId.Value));
        var students = await asyncExecuter.ToListAsync(studentQuery, cancellationToken);
        var studentMap = students.ToDictionary(student => student.Id);
        var marks = records.Where(mark => studentMap.ContainsKey(mark.StudentId))
            .Select(mark => new ExamMark(mark.Id, exam.Id, mark.StudentId, mark.SubjectId, mark.DivisionId,
                mark.MarksObtained, mark.IsAbsent, mark.Feedback, mark.TenantId)).ToList();
        var applicable = resolveApplicability
            ? await subjectApplicabilityResolver.ResolveAsync(exam, students, readOnly: true)
            : new Dictionary<Guid, HashSet<Guid>>();
        return new(metadata with { Marks = marks, StudentMap = studentMap, ApplicableSubjectIdsByStudent = applicable },
            records.ToDictionary(mark => mark.Id, mark => mark.Ordinal));
    }

    private sealed record ExamExportMarkRecord(Guid Id, Guid StudentId, Guid SubjectId, Guid DivisionId,
        double? MarksObtained, bool IsAbsent, string? Feedback, Guid? TenantId, long Ordinal);
    private sealed record ExamExportBatch(ExamReportData Data, Dictionary<Guid, long> Ordinals);
}
