using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Students;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportGenerator
{
    private static readonly string[] ExamExportGrades = ["A+", "A", "B+", "B", "C+", "C", "D", "F", "Absent"];

    private static bool IsExamSummaryExport(string type) => type is AcademicReportTypes.GenderWiseGradeAnalysis or AcademicReportTypes.ResultStatisticsReport
        or AcademicReportTypes.ExamSummaryReport or AcademicReportTypes.AverageSubjectMarkAnalysis or AcademicReportTypes.SubjectAverageClassMarkAnalysis
        or AcademicReportTypes.StudentGradeAnalysisSubjectWise or AcademicReportTypes.ResultListPassFailAnalysisSubjectWise
        or AcademicReportTypes.ResultListSubjectGradeAnalysis or AcademicReportTypes.StudentGradeAnalysis
        or AcademicReportTypes.ResultListPassFailOverallAnalysis or AcademicReportTypes.ResultListOverallGradeAnalysis;

    private static List<AcademicReportColumnDto> ExamSummaryColumns(string type)
    {
        (string Id, string Label)[] columns = type == AcademicReportTypes.GenderWiseGradeAnalysis
            ? [("Gender", "Gender"), ("StudentCount", "Student Count"), ("AverageMarks", "Average Marks"), ("HighestMarks", "Highest Marks"),
                ("LowestMarks", "Lowest Marks"), ("PassPercent", "Pass %"), ("GradeA", "Grade A (≥80%)"), ("GradeB", "Grade B (60–79%)"),
                ("GradeC", "Grade C (40–59%)"), ("GradeD", "Grade D (30–39%)"), ("GradeF", "Grade F (<30%)")]
            : [("ClassName", "Class"), ("Division", "Division"), ("TotalStudents", "Total Students"), ("Appeared", "Appeared"), ("Passed", "Passed"),
                ("Failed", "Failed"), ("Absent", "Absent"), ("HighestMarks", "Highest Marks"), ("LowestMarks", "Lowest Marks"), ("AverageMarks", "Average Marks"), ("PassPercentage", "Pass %")];
        return columns.Select(column => new AcademicReportColumnDto { Id = column.Id, Label = column.Label }).ToList();
    }

    private async IAsyncEnumerable<Dictionary<string, string?>> ReadExamSummaryExportAsync(AcademicReportRequestDto request,
        Exam? exam, ExamReportData metadata, IQueryable<ExamMark> marks, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var type = request.ReportType;
        if (type is AcademicReportTypes.AverageSubjectMarkAnalysis or AcademicReportTypes.SubjectAverageClassMarkAnalysis
            or AcademicReportTypes.StudentGradeAnalysisSubjectWise or AcademicReportTypes.ResultListPassFailAnalysisSubjectWise or AcademicReportTypes.ResultListSubjectGradeAnalysis)
        {
            await foreach (var row in ReadExamSubjectSummaryRowsAsync(request, exam!, metadata, marks, batchSize, cancellationToken)) yield return row;
            yield break;
        }

        var groups = new Dictionary<(Guid? StandardId, Guid DivisionId, string Label), ExamExportStatistics>();
        var total = 0;
        var rank = 0;
        var source = ReadExamStudentTotalsAsync(marks, batchSize, cancellationToken);
        if (type == AcademicReportTypes.ResultListOverallGradeAnalysis)
            source = AcademicReportExternalSort.SortAsync(ReadExamGradeRankTotalsAsync(request, batchSize, cancellationToken), (left, right) =>
            {
                var order = right.Obtained.CompareTo(left.Obtained);
                return order != 0 ? order : left.Ordinal.CompareTo(right.Ordinal);
            }, cancellationToken: cancellationToken);
        await foreach (var student in source.WithCancellation(cancellationToken))
        {
            if (type is AcademicReportTypes.StudentGradeAnalysis or AcademicReportTypes.ResultListOverallGradeAnalysis && !student.Exists) continue;
            total++;
            rank++;
            var label = type switch
            {
                AcademicReportTypes.GenderWiseGradeAnalysis => student.Gender == GenderEnum.Male ? "Male" : student.Gender == GenderEnum.Female ? "Female" : student.Gender == null ? "Unknown" : "Other",
                AcademicReportTypes.StudentGradeAnalysis or AcademicReportTypes.ResultListOverallGradeAnalysis => student.IsAbsent ? "Absent" : ComputeGrade(student.Obtained, metadata.TotalMaxMarks, metadata.DefaultGradeRanges),
                _ => string.Empty
            };
            var key = type == AcademicReportTypes.ResultStatisticsReport ? (student.StandardId, student.DivisionId, label) : ((Guid?)null, Guid.Empty, label);
            if (!groups.TryGetValue(key, out var stats)) groups[key] = stats = new ExamExportStatistics();
            var needsPositivePass = type is AcademicReportTypes.GenderWiseGradeAnalysis or AcademicReportTypes.ResultStatisticsReport;
            stats.Add(student.Obtained, student.IsAbsent, !student.IsAbsent && (!needsPositivePass || metadata.TotalPassMarks > 0) && student.Obtained >= metadata.TotalPassMarks);
            stats.MinRank = Math.Min(stats.MinRank, rank);
            stats.MaxRank = rank;
            if (type == AcademicReportTypes.GenderWiseGradeAnalysis)
            {
                var percentage = metadata.TotalMaxMarks <= 0 ? -1 : student.Obtained / metadata.TotalMaxMarks * 100;
                var grade = student.IsAbsent ? "F" : percentage < 0 ? "—" : percentage >= 80 ? "A" : percentage >= 60 ? "B" : percentage >= 40 ? "C" : percentage >= 30 ? "D" : "F";
                stats.Grades[grade] = stats.Grades.GetValueOrDefault(grade) + 1;
            }
        }
        if (total == 0) yield break;
        if (type is AcademicReportTypes.ExamSummaryReport or AcademicReportTypes.ResultListPassFailOverallAnalysis)
        {
            var stats = groups.Values.Single();
            var row = OverallExamStatistics(stats);
            if (type == AcademicReportTypes.ResultListPassFailOverallAnalysis)
            {
                row.Remove("HighestMarks"); row.Remove("LowestMarks"); row.Remove("AverageMarks");
                row["FailPercentage"] = Percent(stats.Present - stats.Passed, stats.Count);
            }
            yield return row;
            yield break;
        }
        if (type == AcademicReportTypes.GenderWiseGradeAnalysis)
        {
            foreach (var label in new[] { "Male", "Female", "Unknown" })
            {
                if (!groups.TryGetValue((null, Guid.Empty, label), out var stats)) continue;
                var row = new Dictionary<string, string?> { ["Gender"] = label, ["StudentCount"] = stats.Count.ToString(),
                    ["AverageMarks"] = stats.Average.ToString("F2"), ["HighestMarks"] = stats.Highest.ToString("F2"), ["LowestMarks"] = stats.Lowest.ToString("F2"),
                    ["PassPercent"] = Percent(stats.Passed, stats.Count) };
                foreach (var grade in new[] { "A", "B", "C", "D", "F" }) row[$"Grade{grade}"] = stats.Grades.GetValueOrDefault(grade).ToString();
                yield return row;
            }
            yield break;
        }
        if (type == AcademicReportTypes.ResultStatisticsReport)
        {
            var (standards, divisions, _, _, _, _) = await LoadLookupsAsync(includeStandards: true, includeDivisions: true);
            foreach (var (key, stats) in groups.OrderBy(group => standards.GetValueOrDefault(group.Key.StandardId ?? Guid.Empty, string.Empty))
                .ThenBy(group => divisions.GetValueOrDefault(group.Key.DivisionId, string.Empty)))
            {
                var row = OverallExamStatistics(stats);
                row["ClassName"] = standards.GetValueOrDefault(key.StandardId ?? Guid.Empty, "—");
                row["Division"] = divisions.GetValueOrDefault(key.DivisionId, "—");
                row["Absent"] = (stats.Count - stats.Present).ToString();
                yield return row;
            }
            yield break;
        }
        foreach (var grade in ExamExportGrades)
        {
            if (!groups.TryGetValue((null, Guid.Empty, grade), out var stats)) continue;
            var row = new Dictionary<string, string?> { ["Grade"] = grade, ["StudentCount"] = stats.Count.ToString(), ["Percentage"] = Percent(stats.Count, total) };
            if (type == AcademicReportTypes.ResultListOverallGradeAnalysis)
                row["RankRange"] = stats.MinRank == stats.MaxRank ? stats.MinRank.ToString() : $"{stats.MinRank}–{stats.MaxRank}";
            else
            {
                row["HighestMarks"] = stats.Present > 0 ? stats.Highest.ToString("F2") : "—";
                row["LowestMarks"] = stats.Present > 0 ? stats.Lowest.ToString("F2") : "—";
            }
            yield return row;
        }
    }

    private async IAsyncEnumerable<ExamExportStudentTotal> ReadExamStudentTotalsAsync(IQueryable<ExamMark> marks,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var studentTracking = studentRepository.DisableTracking();
        var query = marks.GroupBy(mark => mark.StudentId).Select(group => new
        {
            StudentId = group.Key, Obtained = group.Sum(mark => mark.IsAbsent ? 0 : mark.MarksObtained ?? 0),
            IsAbsent = group.All(mark => mark.IsAbsent), DivisionId = group.Select(mark => mark.DivisionId).FirstOrDefault()
        }).OrderBy(row => row.StudentId);
        var students = await studentRepository.GetQueryableAsync();
        for (var offset = 0; ; offset += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var totals = await asyncExecuter.ToListAsync(query.Skip(offset).Take(batchSize), cancellationToken);
            if (totals.Count == 0) yield break;
            var ids = totals.Select(total => total.StudentId).ToList();
            var studentInfo = await asyncExecuter.ToListAsync(students.Where(student => ids.Contains(student.Id))
                .Select(student => new { student.Id, student.StandardId, Gender = student.AdditionalDetail == null ? (GenderEnum?)null : student.AdditionalDetail.Gender }), cancellationToken);
            var lookup = studentInfo.ToDictionary(student => student.Id);
            foreach (var total in totals)
            {
                lookup.TryGetValue(total.StudentId, out var student);
                yield return new ExamExportStudentTotal(total.StudentId, total.Obtained, total.IsAbsent, total.DivisionId, student != null, student?.StandardId, student?.Gender);
            }
            if (totals.Count < batchSize) yield break;
        }
    }

    private async IAsyncEnumerable<Dictionary<string, string?>> ReadExamSubjectSummaryRowsAsync(AcademicReportRequestDto request,
        Exam exam, ExamReportData metadata, IQueryable<ExamMark> marks, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var type = request.ReportType;
        if (type == AcademicReportTypes.StudentGradeAnalysisSubjectWise)
        {
            await foreach (var row in ReadExamSubjectGradeSummaryAsync(request, metadata, cancellationToken)) yield return row;
            yield break;
        }
        marks = marks.Where(mark => !mark.IsAbsent);
        if (request.Filters.SubjectIds is { Count: > 0 }) marks = marks.Where(mark => request.Filters.SubjectIds.Contains(mark.SubjectId));
        var query = marks.GroupBy(mark => new { mark.SubjectId, mark.DivisionId, mark.MarksObtained })
            .Select(group => new { group.Key.SubjectId, group.Key.DivisionId, Marks = group.Key.MarksObtained ?? 0, Count = group.Count() })
            .OrderBy(row => row.SubjectId).ThenBy(row => row.DivisionId).ThenBy(row => row.Marks);
        var maxBySubject = metadata.ExamSubjects.ToDictionary(subject => subject.SubjectId, subject => subject.TotalMarks ?? 0);
        var passBySubject = metadata.ExamSubjects.ToDictionary(subject => subject.SubjectId, subject => subject.PassingMarks ?? 0);
        var groups = new Dictionary<(Guid SubjectId, Guid DivisionId, string Grade), ExamExportStatistics>();
        for (var offset = 0; ; offset += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = await asyncExecuter.ToListAsync(query.Skip(offset).Take(batchSize), cancellationToken);
            if (batch.Count == 0) break;
            foreach (var item in batch)
            {
                var grade = ComputeGrade(item.Marks, maxBySubject.GetValueOrDefault(item.SubjectId), metadata.DefaultGradeRanges);
                var key = (item.SubjectId, type == AcademicReportTypes.SubjectAverageClassMarkAnalysis ? item.DivisionId : Guid.Empty, string.Empty);
                if (!groups.TryGetValue(key, out var stats)) groups[key] = stats = new ExamExportStatistics { TotalMax = maxBySubject.GetValueOrDefault(item.SubjectId) };
                stats.Add(item.Marks, false, item.Marks >= passBySubject.GetValueOrDefault(item.SubjectId), item.Count);
                stats.Grades[grade] = stats.Grades.GetValueOrDefault(grade) + item.Count;
            }
            if (batch.Count < batchSize) break;
        }
        var standards = new Dictionary<Guid, string>();
        var divisions = new Dictionary<Guid, string>();
        if (type == AcademicReportTypes.SubjectAverageClassMarkAnalysis)
            (standards, divisions, _, _, _, _) = await LoadLookupsAsync(includeStandards: true, includeDivisions: true);
        var ordered = type == AcademicReportTypes.SubjectAverageClassMarkAnalysis
            ? groups.OrderBy(group => divisions.GetValueOrDefault(group.Key.DivisionId, string.Empty)).ThenBy(group => metadata.SubjectNames.GetValueOrDefault(group.Key.SubjectId, string.Empty))
            : groups.OrderBy(group => metadata.SubjectNames.GetValueOrDefault(group.Key.SubjectId, string.Empty)).ThenBy(_ => string.Empty);
        foreach (var (key, stats) in ordered)
        {
            var row = new Dictionary<string, string?> { ["Subject"] = metadata.SubjectNames.GetValueOrDefault(key.SubjectId, "—") };
            if (type == AcademicReportTypes.ResultListSubjectGradeAnalysis)
            {
                foreach (var grade in ExamExportGrades.Take(8)) row[$"Grade_{grade.Replace("+", "Plus")}"] = stats.Grades.GetValueOrDefault(grade).ToString();
                yield return row;
                continue;
            }
            if (type != AcademicReportTypes.SubjectAverageClassMarkAnalysis) row["TotalStudents"] = stats.Count.ToString();
            row["PassPercentage"] = Percent(stats.Passed, stats.Count);
            if (type == AcademicReportTypes.ResultListPassFailAnalysisSubjectWise)
            {
                row["Passed"] = stats.Passed.ToString(); row["Failed"] = (stats.Count - stats.Passed).ToString();
                row["FailPercentage"] = Percent(stats.Count - stats.Passed, stats.Count);
            }
            else
            {
                row["TotalMarks"] = stats.TotalMax.ToString("F0"); row["AverageMarks"] = stats.Average.ToString("F2");
                row["HighestMarks"] = stats.Highest.ToString("F2"); row["LowestMarks"] = stats.Lowest.ToString("F2");
                if (type == AcademicReportTypes.SubjectAverageClassMarkAnalysis)
                {
                    row["ClassName"] = standards.GetValueOrDefault(exam.StandardId, "—"); row["Division"] = divisions.GetValueOrDefault(key.DivisionId, "—");
                }
                else row["FailPercentage"] = Percent(stats.Count - stats.Passed, stats.Count);
            }
            yield return row;
        }
    }

    private async IAsyncEnumerable<Dictionary<string, string?>> ReadExamSubjectGradeSummaryAsync(AcademicReportRequestDto request,
        ExamReportData metadata, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var maximum = metadata.ExamSubjects.ToDictionary(subject => subject.SubjectId, subject => subject.TotalMarks ?? 0);
        var groups = new Dictionary<string, ExamExportStatistics>();
        var total = 0;
        using (examMarkRepository.DisableTracking())
        {
            var query = await GetExamExportMarksAsync(request.Filters.ExamId!.Value, request.Filters);
            // TotalMarks is the maximum of the first encountered subject in each grade, not the
            // largest maximum. Keep the legacy source order while retaining only grade accumulators.
            await foreach (var mark in ((IAsyncEnumerable<ExamMark>)query).WithCancellation(cancellationToken))
            {
                if (mark.IsAbsent || (request.Filters.SubjectIds is { Count: > 0 } && !request.Filters.SubjectIds.Contains(mark.SubjectId))) continue;
                var obtained = mark.MarksObtained ?? 0;
                var max = maximum.GetValueOrDefault(mark.SubjectId);
                var grade = ComputeGrade(obtained, max, metadata.DefaultGradeRanges);
                if (!groups.TryGetValue(grade, out var stats)) groups[grade] = stats = new ExamExportStatistics { TotalMax = max };
                stats.Add(obtained, false, false);
                total++;
            }
        }
        foreach (var grade in ExamExportGrades.Take(8))
        {
            if (!groups.TryGetValue(grade, out var stats)) continue;
            yield return new Dictionary<string, string?> { ["Grade"] = grade, ["StudentCount"] = stats.Count.ToString(), ["Percentage"] = Percent(stats.Count, total),
                ["TotalMarks"] = stats.TotalMax.ToString("F0"), ["HighestMarks"] = stats.Highest.ToString("F2"), ["LowestMarks"] = stats.Lowest.ToString("F2") };
        }
    }

    private static Dictionary<string, string?> OverallExamStatistics(ExamExportStatistics stats) => new()
    {
        ["TotalStudents"] = stats.Count.ToString(), ["Appeared"] = stats.Present.ToString(), ["Passed"] = stats.Passed.ToString(),
        ["Failed"] = (stats.Present - stats.Passed).ToString(), ["HighestMarks"] = stats.Highest.ToString("F2"),
        ["LowestMarks"] = stats.Lowest.ToString("F2"), ["AverageMarks"] = stats.Average.ToString("F2"), ["PassPercentage"] = Percent(stats.Passed, stats.Count)
    };

    private static string Percent(int numerator, int denominator) => $"{(denominator > 0 ? Math.Round((double)numerator / denominator * 100, 2) : 0)}%";

    private sealed record ExamExportStudentTotal(Guid StudentId, double Obtained, bool IsAbsent, Guid DivisionId, bool Exists, Guid? StandardId, GenderEnum? Gender, long Ordinal = 0);

    private sealed class ExamExportStatistics
    {
        public int Count { get; private set; }
        public int Present { get; private set; }
        public int Passed { get; private set; }
        public double Sum { get; private set; }
        public double Highest { get; private set; }
        public double Lowest { get; private set; }
        public double Average => Present > 0 ? Math.Round(Sum / Present, 2) : 0;
        public double TotalMax { get; init; }
        public int MinRank { get; set; } = int.MaxValue;
        public int MaxRank { get; set; }
        public Dictionary<string, int> Grades { get; } = [];
        public void Add(double marks, bool absent, bool passed, int count = 1)
        {
            Count += count;
            if (passed) Passed += count;
            if (absent) return;
            Highest = Present == 0 ? marks : Math.Max(Highest, marks);
            Lowest = Present == 0 ? marks : Math.Min(Lowest, marks);
            Present += count;
            Sum += marks * count;
        }
    }
}
