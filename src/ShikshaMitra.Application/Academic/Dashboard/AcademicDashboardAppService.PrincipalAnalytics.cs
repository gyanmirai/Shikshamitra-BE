using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Results;

namespace ShikshaMitra.Academic.Dashboard;

public partial class AcademicDashboardAppService
{
    public async Task<PrincipalAcademicDashboardAnalyticsDto> GetPrincipalAnalyticsAsync()
    {
        var progressList = await GetDashboardChapterProgressAsync();
        var divisions = await _divisionRepository.GetListAsync();
        var standards = await _standardRepository.GetListAsync();
        var chartStandards = new List<string>();
        var chartPercentages = new List<int>();
        foreach (var standard in standards.OrderBy(item => item.Name).Take(6))
        {
            var divisionIds = divisions.Where(item => item.StandardId == standard.Id)
                .Select(item => item.Id).ToHashSet();
            var progress = progressList.Where(item => divisionIds.Contains(item.DivisionId)).ToList();
            chartStandards.Add(standard.Name);
            chartPercentages.Add(progress.Count > 0
                ? (int)Math.Round(progress.Average(item => item.Progress)) : 0);
        }

        var exams = await _examRepository.GetListAsync();
        var examResultCounts = await GetDashboardResultCountsAsync();
        var countsByExam = examResultCounts.GroupBy(item => item.ExamId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var subjectNames = new List<string>();
        var passCounts = new List<int>();
        var failCounts = new List<int>();
        foreach (var exam in exams.Take(6))
        {
            subjectNames.Add(exam.ExamName);
            countsByExam.TryGetValue(exam.Id, out var counts);
            passCounts.Add(counts?.Where(item => item.ResultStatus == ExamStudentResultStatus.Pass)
                .Sum(item => item.Count) ?? 0);
            failCounts.Add(counts?.Where(item => item.ResultStatus == ExamStudentResultStatus.Fail)
                .Sum(item => item.Count) ?? 0);
        }

        var resultQuery = await _examResultDetailRepository.GetQueryableAsync();
        var topResults = await AsyncExecuter.ToListAsync(resultQuery
            .OrderByDescending(item => item.Percentage).Take(3)
            .Select(item => new { item.Id, item.StudentId, item.Percentage }));
        var bottomResults = await AsyncExecuter.ToListAsync(resultQuery
            .OrderBy(item => item.Percentage).Take(3)
            .Select(item => new { item.Id, item.StudentId, item.Percentage }));
        var studentIds = topResults.Select(item => item.StudentId)
            .Concat(bottomResults.Select(item => item.StudentId)).Distinct().ToList();
        var studentNames = studentIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _studentRepository.GetListAsync(student => studentIds.Contains(student.Id)))
                .ToDictionary(student => student.Id,
                    student => $"{student.FirstName} {student.LastName}".Trim());

        return new PrincipalAcademicDashboardAnalyticsDto
        {
            ChapterCompletionChart = new AcademicDashboardChapterCompletionChartDto
            {
                StandardNames = chartStandards,
                CompletionPercentages = chartPercentages
            },
            SubjectPassFailChart = new AcademicDashboardPassFailChartDto
            {
                SubjectNames = subjectNames,
                PassCounts = passCounts,
                FailCounts = failCounts
            },
            TopPerformers = studentNames.Count == 0 ? [] : topResults.Select(item => new AcademicDashboardPerformerDto
            {
                Id = item.Id.ToString(), Name = studentNames.GetValueOrDefault(item.StudentId, string.Empty),
                AvatarUrl = string.Empty, TotalMarks = (int)item.Percentage
            }).ToList(),
            BottomPerformers = studentNames.Count == 0 ? [] : bottomResults.Select(item => new AcademicDashboardPerformerDto
            {
                Id = item.Id.ToString(), Name = studentNames.GetValueOrDefault(item.StudentId, string.Empty),
                AvatarUrl = string.Empty, TotalMarks = (int)item.Percentage
            }).ToList()
        };
    }
}
