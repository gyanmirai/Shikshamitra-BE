using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Audit;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.Academic.Setup;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Authorization.Permissions;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Academics;
using ClassTeacherEntity = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Dashboard;

[RemoteService(false)]
public class AcademicInsightsOperationalReader(IRepository<AcademicCalendar, Guid> calendars,
	IRepository<AcademicCalendarDay, Guid> days, IAcademicAuditTrailAppService audits,
	AcademicSetupAccess setupAccess, AcademicSetupConfigurationReader configuration, IRepository<AcademicYear, Guid> years,
	IRepository<HrReportExportJob, Guid> exports, IRepository<ClassTeacherEntity, Guid> teachers,
	IRepository<Employee, Guid> employees, IPermissionChecker permissions) : ApplicationService
{
	public async Task<bool> IsClassTeacherAsync(AcademicInsightsContext c)
	{
		var employeeIds = (await employees.GetQueryableAsync()).Where(x => x.TenantId == c.TenantId && x.UserId == CurrentUser.Id).Select(x => x.Id);
		return await AsyncExecuter.AnyAsync(AcademicInsightsClassTeacherQuery.ForContext(await teachers.GetQueryableAsync(), c)
			.Where(x => employeeIds.Contains(x.TeacherId)));
	}
	public async Task<AcademicInsightSectionDto> CalendarAsync(AcademicInsightsContext c)
	{
		var ids = c.DivisionIds; var standards = c.StandardIds;
		var calendarIds = (await calendars.GetQueryableAsync()).Where(x => x.TenantId == c.TenantId && x.AcademicYearId == c.Input.AcademicYearId && x.AcademicMediumId == c.Input.EducationMediumId).Select(x => x.Id);
		var today = Mobile.MobileAcademicContextAppService.SchoolToday;
		var query = (await days.GetQueryableAsync()).Where(x => x.TenantId == c.TenantId && calendarIds.Contains(x.AcademicCalendarId) && x.Date >= today &&
			!(x.DayType == DayType.Holiday && x.Description == AcademicInsightsCalendarPreview.GeneratedWeekendDescription) &&
			(x.AppliesToAll || x.DivisionId.HasValue && Enumerable.Contains(ids, x.DivisionId.Value) || !x.DivisionId.HasValue && x.StandardId.HasValue && Enumerable.Contains(standards, x.StandardId.Value)));
		var total = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.Date).ThenBy(x => x.Id).Take(5).Select(x => new AcademicInsightItemDto(x.Id.ToString(), x.Title, x.DayType.ToString(), x.Date, x.Status.ToString(), null)));
		return new() { Key = "calendar", Status = total == 0 ? "empty" : "ready", Summary = "Upcoming calendar entries for the selected academic scope, excluding recurring weekend holidays; showing up to five.",
			Metrics = [new("upcoming", "Upcoming calendar entries", total)], Items = items };
	}
	public async Task<AcademicInsightSectionDto?> SettingsAsync(AcademicInsightsContext c)
	{
		var settings = await setupAccess.ReadPermissionsAsync();
		if (!settings.Any(x => x.CanView)) return null;
		var year = await years.GetAsync(x => x.Id == c.Input.AcademicYearId && x.TenantId == c.TenantId);
		await configuration.ReadAsync(settings, new(year, c.Input.EducationMediumId, c.Input.StandardId));
		var visible = settings.Where(x => x.CanView).ToList();
		return new() { Key = "settings", Status = "ready", Summary = "Existence of the nine Academic setup settings you can view. Existence does not establish operational readiness; class-specific settings require a selected class.",
			Series = [new("existence", visible.Where(x => x.ConfigurationState is "configured" or "empty").Select(x => new AcademicInsightPointDto(x.Key, x.Key, x.ConfigurationState == "configured" ? 1 : 0, Status: x.ConfigurationState)).ToList())],
			Items = visible.Select(x => new AcademicInsightItemDto(x.Key, x.Key, Status: x.ConfigurationState)).ToList() };
	}
	public async Task<AcademicInsightSectionDto> AuditsAsync(AcademicInsightsContext c)
	{
		if (!c.WholeMedium) return AcademicInsightsAppService.Unavailable("audits", "The audit source cannot safely summarize individual classes or divisions.");
		var result = await audits.GetListAsync(new() { AcademicYearId = c.Input.AcademicYearId, AcademicMediumId = c.Input.EducationMediumId, MaxResultCount = 10, Sorting = "occurredAt desc" });
		return new() { Key = "audits", Status = result.TotalCount == 0 ? "empty" : "ready", Summary = "Recent academic audit events in the selected medium; showing up to 10.",
			Metrics = [new("events", "Academic audit events", result.TotalCount)],
			Items = result.Items.Select(x => new AcademicInsightItemDto(x.Id.ToString(), x.ActionLabel, x.Summary, x.OccurredAtUtc, x.SubModuleCode)).ToList() };
	}
	public async Task<AcademicInsightSectionDto?> ReportsAsync(AcademicInsightsContext c)
	{
		(string Module, string Permission)[] policies = [(AcademicReportModules.Curriculum, P.CurriculumManagement_Reports.View),
			(AcademicReportModules.Timetable, P.Timetable_Reports.View), (AcademicReportModules.HomeworkAssignment, P.HomeworkAndAssignment_Reports.View),
			(AcademicReportModules.Examination, P.Examination_Reports.View)];
		var allowed = new System.Collections.Generic.List<string>();
		foreach (var policy in policies) if (await permissions.IsGrantedAsync(policy.Permission)) allowed.Add(policy.Module);
		if (allowed.Count == 0) return null;
		if (!c.WholeMedium) return AcademicInsightsAppService.Unavailable("reports", "Stored export jobs do not preserve a verifiable class and division authorization snapshot.");
		var jobs = await AsyncExecuter.ToListAsync((await exports.GetQueryableAsync()).Where(x => x.TenantId == c.TenantId &&
			x.RequestedByUserId == CurrentUser.Id && x.Status == HrReportExportStatus.Completed).OrderByDescending(x => x.CreationTime).Take(100)
			.Select(x => new { x.ReportType, x.RequestJson }));
		var matched = jobs.Select(x => new { x.ReportType, Request = AcademicReportAppService.TryReadDynamicRequest(x.RequestJson) })
			.Where(x => x.Request != null && x.Request.AcademicYearId == c.Input.AcademicYearId && x.Request.EducationBoardId == c.Input.BoardId &&
				x.Request.EducationMediumId == c.Input.EducationMediumId && allowed.Contains(x.Request.Module)).ToList();
		return new() { Key = "reports", Status = matched.Count == 0 ? "empty" : "ready", Summary = "Completed exports in permitted Academic report families among your latest 100 completed jobs with a verified selected context. Legacy jobs without that context are excluded.",
			Series = [new("completed-exports", matched.GroupBy(x => x.Request!.Module).Select(g => new AcademicInsightPointDto(g.Key, g.Key, g.Count())).ToList())],
			Metrics = [new("completed-exports", "Completed exports in preview window", matched.Count)] };
	}
}
