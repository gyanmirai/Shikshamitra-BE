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
        var overview = await BuildPrincipalDashboardAsync(
            includeAnalytics: false, includePerformance: false);
        return new PrincipalAcademicDashboardOperationsDto
        {
            ReadinessRate = overview.ReadinessRate,
            EvaluationLag = overview.EvaluationLag,
            UpcomingExams = overview.UpcomingExams,
            Insights = overview.Insights,
            ReadinessItems = overview.ReadinessItems,
            ActionCenterItems = overview.ActionCenterItems,
            DataHealthItems = overview.DataHealthItems
        };
    }
}
