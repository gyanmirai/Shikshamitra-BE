using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Dashboard;

[RemoteService(false)]
public class DashboardOperationalFocusReader(IStudentListAppService studentList, AdmissionApplicationScopeAccessService admissions,
	IRepository<AdmissionCycle, Guid> cycles, IRepository<AdmissionOffering, Guid> offerings,
	DashboardTeacherScopeResolver teacher, IRepository<Timetable, Guid> timetables, StudentScopeAccessService scope,
	IRepository<Division, Guid> divisions, IRepository<Standard, Guid> standards, IRepository<Subject, Guid> subjects,
	AdmissionDashboardQueryService admissionDashboard) : ApplicationService
{
	public async Task FillAttendanceAsync(DashboardFocusDto focus, DashboardSummaryDto summary, Guid year)
	{
		var data = await studentList.GetAttendanceDashboardSummaryAsync(new() { AcademicYearId = year,
			StartDate = DateTime.ParseExact(summary.DateRange.From!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
			EndDate = DateTime.ParseExact(summary.DateRange.To!, "yyyy-MM-dd", CultureInfo.InvariantCulture) });
		focus.Status = data.Classes.Count == 0 ? "empty" : "ready";
		focus.Message = data.IsProvisional ? "Attendance is provisional because expected registers are incomplete." : null;
		focus.Series = [new("classes", "Class attendance", data.Classes.OrderBy(x => x.Name).Take(30)
			.Select(x => new DashboardFocusPointDto(x.StandardId?.ToString() ?? x.Name, x.Name, x.Percentage)).ToList())];
		focus.Metrics = [new("attendance", "Student attendance", data.SchoolAttendancePercentage, "percent"), new("expected", "Expected student-days", data.ExpectedDays, "count"), new("recorded", "Recorded student-days", data.RecordedDays, "count"), new("missing", "Missing student-days", data.MissingDays, "count")];
		focus.Description = "Policy-based class attendance for the selected year and range. Showing up to 30 permitted classes.";
	}

	public async Task FillAdmissionsAsync(DashboardFocusDto focus, Guid year)
	{
		var query = await GetScopedYearApplicationsAsync(year);
		var rows = await AsyncExecuter.ToListAsync(query.GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }));
		focus.Status = rows.Count == 0 ? "empty" : "ready";
		focus.Series = [new("applications", "Applications by current stage", rows.OrderBy(x => x.Status).Select(x => new DashboardFocusPointDto(x.Status.ToString(), Split(x.Status.ToString()), x.Count)).ToList())];
		focus.Metrics = [new("applications", "Applications in scope", rows.Sum(x => x.Count), "count")];
	}

	public async Task<int> CountPendingDecisionsAsync(Guid year)
	{
		var query = await GetScopedYearApplicationsAsync(year);
		var pending = await admissionDashboard.ApplyAsync(query, false, true, null);
		return await AsyncExecuter.CountAsync(pending);
	}

	private async Task<IQueryable<AdmissionApplication>> GetScopedYearApplicationsAsync(Guid year)
	{
		var yearCycles = (await cycles.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == year);
		var yearOfferings = (await offerings.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && yearCycles.Any(c => c.Id == x.AdmissionCycleId));
		return (await admissions.GetScopedApplicationsAsync()).Where(x => x.TenantId == CurrentTenant.Id && yearOfferings.Any(o => o.Id == x.AdmissionOfferingId));
	}

	public async Task FillTimetableAsync(DashboardFocusDto focus, Guid year)
	{
		var resolved = await teacher.ResolveAsync();
		if (!resolved.IsResolved || !resolved.EmployeeId.HasValue) { focus.Message = "No unique active teaching profile is linked to this login."; return; }
		var allowed = await scope.ApplyCurrentUserDivisionScopeAsync(await divisions.GetQueryableAsync(), year);
		var standardQuery = await standards.GetQueryableAsync();
		var query = from timetable in await timetables.GetQueryableAsync()
			where timetable.TenantId == CurrentTenant.Id && timetable.Status == TimetableStatus.Published && allowed.Any(d => d.Id == timetable.DivisionId && d.StandardId == timetable.StandardId)
			from slot in timetable.Slots
			from subject in slot.Subjects
			join standard in standardQuery on timetable.StandardId equals standard.Id
			join division in allowed on timetable.DivisionId equals division.Id
			join subjectInfo in await subjects.GetQueryableAsync() on subject.SubjectId equals subjectInfo.Id
			where slot.SlotType == SlotType.Period && (subject.TeacherId == resolved.EmployeeId || subject.TeacherId == CurrentUser.Id)
			orderby slot.Day, slot.StartTime, timetable.Id, subject.Id
			select new { subject.Id, slot.Day, slot.StartTime, slot.EndTime, SubjectName = subjectInfo.Name, ClassName = standard.Name, DivisionName = division.Name };
		var rows = await AsyncExecuter.ToListAsync(query.Take(61));
		if (rows.Count > 60) { focus.Message = "More than 60 weekly lessons match. Open Timetable for the full schedule."; return; }
		focus.Status = rows.Count == 0 ? "empty" : "ready";
		focus.Items = rows.Select(x => new DashboardFocusItemDto { Id = x.Id.ToString(), Title = x.SubjectName,
			Subtitle = $"{x.Day} · {x.ClassName} {x.DivisionName}", Time = x.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture), EndTime = x.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture), Status = "published" }).ToList();
		focus.Series = [new("week", "Published lessons by weekday", rows.GroupBy(x => x.Day).OrderBy(x => x.Key).Select(g => new DashboardFocusPointDto(g.Key.ToString(), g.Key.ToString(), g.Count())).ToList())];
		focus.PeriodLabel = "Published weekly schedule";
	}

	private static string Split(string text) => System.Text.RegularExpressions.Regex.Replace(text, "([a-z])([A-Z])", "$1 $2");
}
