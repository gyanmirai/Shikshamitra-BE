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
    private async Task<AcademicReportPreviewDto?> TryBuildExamMarkPreviewAsync(AcademicReportRequestDto request, AcademicReportPreviewDto payload)
    {
        if (request.ReportType is not (AcademicReportTypes.SubjectwiseMarksReport or AcademicReportTypes.SubjectWiseGradeList
            or AcademicReportTypes.ResultListWithSubject or AcademicReportTypes.ResultListWithSubjectFailedDetails
            or AcademicReportTypes.ResultListWithSubjectGrade or AcademicReportTypes.ResultListWithSubjectWithGrace
            or AcademicReportTypes.BrightStudentListSubjectWise or AcademicReportTypes.WeakStudentAnalysisSubjectWise)) return null;
        using var examTracking = examRepository.DisableTracking();
        using var gradeTracking = gradingScaleRepository.DisableTracking();
        var examQuery = await examRepository.WithDetailsAsync(exam => exam.ExamSubjects);
        var exam = request.Filters.ExamId.HasValue
            ? await asyncExecuter.FirstOrDefaultAsync(examQuery.Where(exam => exam.Id == request.Filters.ExamId.Value)) : null;
        var data = exam == null ? new ExamReportData([], 0, 0, [], [], [], [], null) : await CreateExamExportMetadataAsync(exam, CancellationToken.None);
        await FormatExamExportBatchAsync(request, payload, data);
        if (exam == null) return payload;
        var marks = await GetExamExportMarksAsync(exam.Id, request.Filters);
        if (!await asyncExecuter.AnyAsync(marks)) return payload;
        var order = (await asyncExecuter.ToListAsync(marks.Select(mark => mark.Id)))
            .Select((id, index) => (Id: id, Index: index)).ToDictionary(item => item.Id, item => item.Index);
        var students = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
        var raw = await asyncExecuter.ToListAsync(from mark in marks join student in students on mark.StudentId equals student.Id
            select new
            {
                mark.Id, mark.StudentId, mark.SubjectId, mark.MarksObtained, mark.IsAbsent,
                student.RollNo, student.FirstName, student.MiddleName, student.LastName
            });
        var maximum = data.ExamSubjects.ToDictionary(subject => subject.SubjectId, subject => subject.TotalMarks ?? 0);
        var passing = data.ExamSubjects.ToDictionary(subject => subject.SubjectId, subject => subject.PassingMarks ?? 0);
        var keys = raw.Select(mark => new ExamPreviewMarkKey
        {
            Id = mark.Id, StudentId = mark.StudentId, SubjectId = mark.SubjectId, RollNo = mark.RollNo,
            Name = string.Join(" ", new[] { mark.FirstName, mark.MiddleName, mark.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))).Trim(),
            Obtained = mark.MarksObtained ?? 0, IsAbsent = mark.IsAbsent, Ordinal = order[mark.Id],
            Grade = ComputeGrade(mark.MarksObtained ?? 0, maximum.GetValueOrDefault(mark.SubjectId), data.DefaultGradeRanges),
            Subject = data.SubjectNames.GetValueOrDefault(mark.SubjectId, string.Empty),
            DisplaySubject = data.SubjectNames.GetValueOrDefault(mark.SubjectId, "—")
        }).OrderBy(key => key.Ordinal).ToList();
        IEnumerable<ExamPreviewMarkKey> filtered = keys;
        if (request.ReportType is not (AcademicReportTypes.SubjectwiseMarksReport or AcademicReportTypes.ResultListWithSubject))
            filtered = filtered.Where(key => !key.IsAbsent);
        if (request.ReportType is not (AcademicReportTypes.ResultListWithSubject or AcademicReportTypes.ResultListWithSubjectFailedDetails)
            && request.Filters.SubjectIds is { Count: > 0 }) filtered = filtered.Where(key => request.Filters.SubjectIds.Contains(key.SubjectId));
        var subjectOrder = filtered.GroupBy(key => key.SubjectId).ToDictionary(group => group.Key, group => group.First().Ordinal);
        if (request.ReportType is AcademicReportTypes.SubjectWiseGradeList or AcademicReportTypes.BrightStudentListSubjectWise)
            filtered = filtered.Where(key => string.IsNullOrWhiteSpace(request.Filters.Grade) || key.Grade.Equals(request.Filters.Grade, StringComparison.OrdinalIgnoreCase));
        if (request.ReportType is AcademicReportTypes.ResultListWithSubjectFailedDetails or AcademicReportTypes.WeakStudentAnalysisSubjectWise)
            filtered = filtered.Where(key => key.Obtained < passing.GetValueOrDefault(key.SubjectId));
        var filteredKeys = filtered.ToList();
        var rankByMark = new Dictionary<Guid, int>();
        if (request.ReportType is AcademicReportTypes.ResultListWithSubjectGrade or AcademicReportTypes.BrightStudentListSubjectWise)
            foreach (var group in filteredKeys.GroupBy(key => key.SubjectId))
            {
                var rank = 0;
                var ranked = group.OrderByDescending(key => key.Obtained).ToList();
                if (request.ReportType == AcademicReportTypes.ResultListWithSubjectGrade) _ = ranked.ToDictionary(key => key.StudentId);
                foreach (var key in ranked) rankByMark[key.Id] = ++rank;
            }
        var sorted = request.ReportType switch
        {
            AcademicReportTypes.SubjectwiseMarksReport or AcademicReportTypes.ResultListWithSubject =>
                filteredKeys.OrderBy(key => key.RollNo).ThenBy(key => key.Subject),
            AcademicReportTypes.ResultListWithSubjectGrade => filteredKeys.OrderBy(key => key.Subject).ThenByDescending(key => key.Obtained),
            AcademicReportTypes.BrightStudentListSubjectWise => filteredKeys.OrderBy(key => key.Subject)
                .ThenBy(key => subjectOrder[key.SubjectId]).ThenByDescending(key => key.Obtained),
            AcademicReportTypes.ResultListWithSubjectWithGrace => filteredKeys.OrderBy(key => key.Name).ThenBy(key => key.Subject),
            _ => filteredKeys.OrderBy(key => key.Name).ThenBy(key => key.DisplaySubject)
        };
        var selected = sorted.Skip(PreviewSkip(request)).Take(request.PageSize).ToList();
        payload.TotalCount = filteredKeys.Count; payload.Page = request.Page; payload.PageSize = request.PageSize;
        if (selected.Count == 0) return payload;
        var ids = selected.Select(key => key.Id).ToList();
        var records = await asyncExecuter.ToListAsync(marks.Where(mark => ids.Contains(mark.Id)).Select(mark => new ExamPreviewMark
        {
            Id = mark.Id, StudentId = mark.StudentId, SubjectId = mark.SubjectId, DivisionId = mark.DivisionId,
            Obtained = mark.MarksObtained, IsAbsent = mark.IsAbsent, Feedback = mark.Feedback, TenantId = mark.TenantId
        }).Take(request.PageSize));
        var exportRecords = records.OrderBy(mark => ids.IndexOf(mark.Id)).Select((mark, ordinal) => new ExamExportMarkRecord(mark.Id,
            mark.StudentId, mark.SubjectId, mark.DivisionId, mark.Obtained, mark.IsAbsent, mark.Feedback, mark.TenantId, ordinal)).ToList();
        var batch = await LoadExamExportBatchAsync(exam, data, request.Filters, false, exportRecords, CancellationToken.None);
        var result = await FormatExamExportBatchAsync(request, payload, batch.Data);
        for (var index = 0; index < result.Rows.Count; index++)
            if (rankByMark.TryGetValue(selected[index].Id, out var rank)) result.Rows[index]["Rank"] = rank.ToString();
        if (request.ReportType == AcademicReportTypes.SubjectwiseMarksReport)
        {
            var studentIds = selected.Select(key => key.StudentId).Distinct().ToList();
            var totals = await asyncExecuter.ToListAsync(marks.Where(mark => studentIds.Contains(mark.StudentId)).GroupBy(mark => mark.StudentId)
                .Select(group => new { StudentId = group.Key, Total = group.Sum(mark => mark.IsAbsent ? 0 : mark.MarksObtained ?? 0) }));
            var totalMap = totals.ToDictionary(total => total.StudentId, total => total.Total);
            for (var index = 0; index < result.Rows.Count; index++) result.Rows[index]["TotalMarks"] = totalMap[selected[index].StudentId].ToString("F2");
        }
        result.TotalCount = filteredKeys.Count; result.Page = request.Page; result.PageSize = request.PageSize;
        return result;
    }

    private sealed class ExamPreviewMarkKey
    {
        public Guid Id { get; init; }
        public Guid StudentId { get; init; }
        public Guid SubjectId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Subject { get; init; } = string.Empty;
        public string DisplaySubject { get; init; } = string.Empty;
        public int? RollNo { get; init; }
        public double Obtained { get; init; }
        public bool IsAbsent { get; init; }
        public string Grade { get; init; } = string.Empty;
        public int Ordinal { get; init; }
    }
}
