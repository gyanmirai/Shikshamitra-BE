using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;

namespace ShikshaMitra.Academic.Dashboard;

public partial class AcademicDashboardAppService
{
    public async Task<AdminAcademicDashboardAnalyticsDto> GetAdminAnalyticsAsync()
    {
        await EnsureAdminDashboardAccessAsync();

        var examQuery = await _examRepository.GetQueryableAsync();
        var exams = await AsyncExecuter.ToListAsync(examQuery.Select(exam =>
            new DashboardExamSummary(exam.Id, exam.ExamName, exam.Status, exam.CreationTime)));
        var resultCounts = await GetDashboardResultCountsAsync();
        var groupedResults = resultCounts
            .GroupBy(result => result.ExamId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var subjectNames = new List<string>();
        var passCounts = new List<int>();
        var failCounts = new List<int>();

        var latestExams = exams.OrderByDescending(exam => exam.CreationTime).Take(6).ToList();
        foreach (var exam in latestExams)
        {
            subjectNames.Add(exam.ExamName);
            if (groupedResults.TryGetValue(exam.Id, out var examResults))
            {
                passCounts.Add(examResults
                    .Where(result => result.ResultStatus == ExamStudentResultStatus.Pass)
                    .Sum(result => result.Count));
                failCounts.Add(examResults
                    .Where(result => result.ResultStatus == ExamStudentResultStatus.Fail)
                    .Sum(result => result.Count));
            }
            else
            {
                passCounts.Add(0);
                failCounts.Add(0);
            }
        }

        var progressList = await GetDashboardChapterProgressAsync();
        var classProgressChart = await BuildClassProgressChartAsync(progressList);

        return new AdminAcademicDashboardAnalyticsDto
        {
            RecentActivities = exams
                .OrderByDescending(exam => exam.CreationTime)
                .Take(2)
                .Select(exam => new AcademicDashboardActivityDto
                {
                    Id = exam.Id.ToString(),
                    Action = $"Exam '{exam.ExamName}' scheduled",
                    Actor = "Admin",
                    Time = "Recent",
                    Type = "schedule"
                })
                .ToList(),
            SubjectPassFailChart = new AcademicDashboardPassFailChartDto
            {
                SubjectNames = subjectNames,
                PassCounts = passCounts,
                FailCounts = failCounts
            },
            ClassSyllabusProgressChart = classProgressChart
        };
    }

    private async Task<AcademicDashboardClassProgressChartDto> BuildClassProgressChartAsync(
        IReadOnlyCollection<DashboardChapterProgress> progressList)
    {
        var divisions = await _divisionRepository.GetListAsync();
        var standardIds = divisions.Select(division => division.StandardId).Distinct().ToList();
        var standards = standardIds.Count == 0
            ? []
            : await _standardRepository.GetListAsync(standard => standardIds.Contains(standard.Id));
        var standardNames = standards.ToDictionary(standard => standard.Id, standard => standard.Name);
        var progressByDivision = progressList
            .GroupBy(progress => progress.DivisionId)
            .ToDictionary(
                group => group.Key,
                group => (int)Math.Round(group.Average(progress => progress.Progress)));

        var rows = divisions
            .Select(division => new
            {
                Label = $"{standardNames.GetValueOrDefault(division.StandardId, "Class")} - {division.Name}",
                Completion = progressByDivision.GetValueOrDefault(division.Id, 0)
            })
            .OrderBy(row => row.Label)
            .Take(10)
            .ToList();

        return new AcademicDashboardClassProgressChartDto
        {
            ClassNames = rows.Select(row => row.Label).ToList(),
            CompletionPercentages = rows.Select(row => Math.Clamp(row.Completion, 0, 100)).ToList()
        };
    }
}
