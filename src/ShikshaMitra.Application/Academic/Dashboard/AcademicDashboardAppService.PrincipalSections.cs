using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Common;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Dashboard;

public partial class AcademicDashboardAppService
{
    public async Task<PrincipalAcademicDashboardPerformanceDto> GetPrincipalPerformanceAsync()
    {
        var resultQuery = await _examResultDetailRepository.GetQueryableAsync();
        var averages = await AsyncExecuter.ToListAsync(resultQuery
            .GroupBy(_ => 1)
            .Select(group => new { Count = group.Count(), Average = group.Average(item => item.Percentage) }));
        var average = averages.SingleOrDefault();
        var score = average == null ? 0 : (int)Math.Round(average.Average);
        var grade = average == null ? "N/A" : score switch
        {
            >= 90 => "A+",
            >= 80 => "A",
            >= 70 => "B+",
            >= 60 => "B",
            >= 50 => "C",
            _ => "F"
        };

        return new PrincipalAcademicDashboardPerformanceDto
        {
            SchoolPerformanceIndex = score,
            SchoolAverageGrade = grade,
            AverageAttendance = "N/A",
            TotalTeachers = await _employeeRepository.CountAsync(employee =>
                employee.EmployeeGroup == EmployeeGroupType.Teaching),
            TotalStudents = (int)await _studentRepository.GetCountAsync(),
            PerformanceIndexHelper = average == null
                ? "No published result data yet" : "Based on recorded result details"
        };
    }

    public async Task<PrincipalAcademicDashboardCurriculumDto> GetPrincipalCurriculumAsync()
    {
        var progress = await GetDashboardChapterProgressAsync();
        return new PrincipalAcademicDashboardCurriculumDto
        {
            SyllabusProgress = progress.Count == 0
                ? 0 : (int)Math.Round(progress.Average(item => item.Progress))
        };
    }

    public async Task<PrincipalAcademicDashboardOperationsDto> GetPrincipalOperationsAsync()
    {
        var overview = await ReadDashboardSectionAggregatesAsync(teacherScope: false);
        var result = new PrincipalAcademicDashboardOperationsDto
        {
            ReadinessRate = overview.ReadinessItems.Count > 0
                ? (int)Math.Round(overview.ReadinessItems.Average(item => item.TotalCount == 0
                    ? 0 : (double)item.CompletedCount / item.TotalCount * 100)) : 0,
            EvaluationLag = overview.PendingEvaluations > 0
                ? $"{overview.PendingEvaluations} pending" : "No pending evaluations",
            UpcomingExams = overview.UpcomingExams,
            ReadinessItems = overview.ReadinessItems,
            ActionCenterItems = overview.ActionCenterItems,
            DataHealthItems = overview.DataHealthItems
        };
        if (overview.PendingEvaluations > 10)
        {
            result.Insights.Add(new AcademicDashboardInsightDto
            {
                Title = $"{overview.PendingEvaluations} homework evaluations pending review",
                Summary = "Some teachers have pending assessments older than expected.",
                Severity = "warning",
                Source = "evaluations",
                ActionLabel = "Assessment portal",
                ActionHref = "/academic/gradingAndFeedback"
            });
        }
        return result;
    }
}
