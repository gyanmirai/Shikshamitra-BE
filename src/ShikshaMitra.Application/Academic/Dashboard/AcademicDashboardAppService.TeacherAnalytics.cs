using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Dashboard;

public partial class AcademicDashboardAppService
{
    public async Task<TeacherAcademicDashboardAnalyticsDto> GetTeacherAnalyticsAsync()
    {
        var result = new TeacherAcademicDashboardAnalyticsDto();
        if (!CurrentUser.Id.HasValue)
        {
            return result;
        }

        var employees = await _employeeRepository.GetListAsync(employee =>
            employee.UserId == CurrentUser.Id.Value);
        var teacher = employees.FirstOrDefault();
        if (teacher == null)
        {
            return result;
        }

        var assignments = await _assignedSubjectRepository.GetListAsync(assignment =>
            assignment.Teachers.Any(item => item.TeacherId == teacher.Id));
        if (assignments.Count == 0)
        {
            return result;
        }

        var standardIds = assignments.Select(item => item.StandardId).Distinct().ToList();
        var divisions = await _divisionRepository.GetListAsync(division =>
            standardIds.Contains(division.StandardId));
        var divisionIds = divisions.Select(item => item.Id).ToList();
        var results = divisionIds.Count > 0
            ? await GetDashboardResultDetailsAsync(divisionIds: divisionIds)
            : [];
        result.PerformanceDistribution = new TeacherAcademicDashboardPerformanceDistributionDto
        {
            Excellent = results.Count(item => item.Percentage >= 85),
            Good = results.Count(item => item.Percentage >= 70 && item.Percentage < 85),
            Average = results.Count(item => item.Percentage >= 50 && item.Percentage < 70),
            NeedsImprovement = results.Count(item => item.Percentage < 50)
        };

        var assignmentIds = assignments.Select(item => item.Id).ToList();
        var chapters = await _chapterRepository.GetListAsync(chapter =>
            assignmentIds.Contains(chapter.AssignedSubjectId));
        var chartChapters = chapters.OrderBy(chapter => chapter.ChapterNumber).Take(6).ToList();
        if (chartChapters.Count == 0)
        {
            return result;
        }

        var chapterIds = chartChapters.Select(chapter => chapter.Id).ToList();
        var progressQuery = await _chapterProgressRepository.GetQueryableAsync();
        var progressRows = await AsyncExecuter.ToListAsync(progressQuery
            .Where(progress => chapterIds.Contains(progress.ChapterId))
            .Select(progress => new { progress.ChapterId, progress.Progress }));
        var progressByChapter = progressRows.GroupBy(item => item.ChapterId)
            .ToDictionary(group => group.Key,
                group => (int)Math.Round(group.Average(item => item.Progress)));
        result.ChapterProgressChart = new AcademicDashboardChapterCompletionChartDto
        {
            StandardNames = chartChapters.Select(chapter => chapter.ChapterName).ToList(),
            CompletionPercentages = chartChapters.Select(chapter =>
                progressByChapter.GetValueOrDefault(chapter.Id, 0)).ToList()
        };
        return result;
    }
}
