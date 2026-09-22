using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Common;

namespace ShikshaMitra.Academic.Dashboard;

public partial class AcademicDashboardAppService
{
    public async Task<AdminAcademicDashboardTimetableDto> GetAdminTimetableAsync()
    {
        await EnsureAdminDashboardAccessAsync();

        var timetableQuery = await _timetableRepository.GetQueryableAsync();
        var timetableStatuses = await AsyncExecuter.ToListAsync(
            timetableQuery.Select(timetable => timetable.Status));
        var divisionCount = (int)await _divisionRepository.GetCountAsync();
        var publishedTimetables = timetableStatuses.Count(status => status == TimetableStatus.Published);
        var pendingTimetables = timetableStatuses.Count(status => status == TimetableStatus.Draft);
        var timetableRate = divisionCount > 0
            ? (int)Math.Round((double)publishedTimetables / divisionCount * 100)
            : 0;

        var result = new AdminAcademicDashboardTimetableDto
        {
            TimetableRate = timetableRate,
            ActiveTimetables = publishedTimetables,
            PendingTimetables = pendingTimetables,
            ConflictsCount = 0,
            ConfigurationStatus =
            [
                new() { Label = "Timetable", Status = publishedTimetables > 0 ? "active" : "missing", Path = "/academic/newTimetable", Icon = "solar:calendar-bold" },
                new() { Label = "Weekly Lectures", Status = timetableStatuses.Count > 0 ? "configured" : "missing", Path = "/academic/timetableSettings", Icon = "solar:calendar-date-bold" },
                new() { Label = "Lecture Slots", Status = timetableStatuses.Count > 0 ? "configured" : "missing", Path = "/academic/timetableSettings", Icon = "solar:calendar-date-bold" }
            ],
            OperationalStatus =
            [
                new()
                {
                    Module = "Timetable Setup",
                    Status = $"Pending for {pendingTimetables} Classes",
                    Level = pendingTimetables > 0 ? "warning" : "success",
                    Progress = timetableRate,
                    Icon = "solar:calendar-bold"
                }
            ]
        };

        if (pendingTimetables > 0)
        {
            result.Insights.Add(new AcademicDashboardInsightDto
            {
                Title = "Timetable configuration pending",
                Summary = $"There are {pendingTimetables} class timetables currently in draft status.",
                Severity = "critical",
                Source = "timetable",
                ActionLabel = "Resolve",
                ActionHref = "/academic/newTimetable"
            });
        }

        return result;
    }
}
