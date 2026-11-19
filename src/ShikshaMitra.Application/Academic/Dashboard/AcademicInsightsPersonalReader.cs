using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Mobile;
using ShikshaMitra.Academic.Timetable.Portal;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Dashboard;

[RemoteService(false)]
public class AcademicInsightsPersonalReader(StudentPortalAccessService portal, MobileAcademicContextAppService context,
	IMobileDashboardAppService dashboard, IMobileHomeworkAppService homework, IMobileSyllabusAppService syllabus,
	IMobileResultAppService results, IRepository<EducationMedium, Guid> mediums, IPortalTimetableAppService timetables,
	IMobileCalendarAppService calendar) : ApplicationService
{
	public async Task<AcademicInsightsDto> ReadAsync(AcademicInsightsInput input)
	{
		var links = await portal.GetMyStudentsAsync();
		if (links.Count == 0) throw new AbpAuthorizationException();
		var students = links.GroupBy(x => x.StudentId).Select(g => new AcademicInsightStudentDto(g.Key, g.First().StudentName)).ToList();
		var response = new AcademicInsightsDto { Role = links.Any(x => x.AccountType == "guardian") ? "parent" : "student",
			GeneratedAt = Clock.Now.ToUniversalTime(), Students = students };
		if (students.Count > 1 && !input.StudentId.HasValue) { response.ScopeLabel = "Select a student"; return response; }
		var selected = input.StudentId ?? students.Single().Id;
		if (!students.Any(x => x.Id == selected)) throw new AbpAuthorizationException();
		var resolved = new MobileAcademicInput { StudentId = selected, AcademicYearId = input.AcademicYearId };
		var placement = await context.ResolveAsync(resolved);
		if (placement.MediumId != input.EducationMediumId || placement.Year.Id != input.AcademicYearId ||
			input.StandardId.HasValue && input.StandardId != placement.StandardId || input.DivisionId.HasValue && input.DivisionId != placement.DivisionId ||
			!await mediums.AnyAsync(x => x.Id == input.EducationMediumId && x.TenantId == CurrentTenant.Id && x.EducationBoardId == input.BoardId))
			throw new BusinessException("Academic:Insights:InvalidContext");
		response.ScopeLabel = $"{MobileAcademicContextAppService.Name(placement.Student)} / {placement.StandardName} / {placement.DivisionName}";
		async Task<T?> Load<T>(string key, Func<Task<T>> read) where T : class
		{
			T? value = null;
			var error = await AcademicInsightsSectionAccess.SafeReadAsync(key, async () => { value = await read(); return null; }, Logger);
			if (error != null && !response.Sections.Any(x => x.Key == key)) response.Sections.Add(error);
			return value;
		}
		var overview = await Load("student360", () => dashboard.GetAsync(resolved));
		var today = MobileAcademicContextAppService.SchoolToday;
		var markers = await Load("calendar", () => calendar.GetAsync(new() { StudentId = selected, AcademicYearId = input.AcademicYearId, Year = today.Year, Month = today.Month }));
		var learning = await Load("curriculum", () => syllabus.GetDashboardAsync(resolved));
		var tasks = await Load("tasks", () => homework.GetListAsync(new() { StudentId = selected, AcademicYearId = input.AcademicYearId, PageSize = 100 }));
		var resultPage = await Load("student360", () => results.GetListAsync(new() { StudentId = selected, AcademicYearId = input.AcademicYearId, PageSize = 100 }));
		var scores = resultPage?.Items.Where(x => x.Status == "published" && x.OverallTotal > 0 && x.Percentage.HasValue).ToList() ?? [];
		var score = scores.Count == 0 ? (double?)null : scores.Average(x => x.Percentage!.Value);
		if (overview != null && resultPage != null) response.Sections.Add(new() { Key = "student360", Status = "ready", Summary = "Verified student portal attendance and published exam results. Exam preview includes up to 100 results.",
			Metrics = [new("attendance", "Attendance", overview.Attendance.CurrentPercentage, "%"), new("exam-score", "Average published exam score in preview", score, "%")],
			Series = [new("attendance-score", overview.Attendance.CurrentPercentage.HasValue && score.HasValue ?
				[new(selected.ToString(), students.Single(x => x.Id == selected).Label, score.Value, overview.Attendance.CurrentPercentage, score)] : [])] });
		if (learning != null) response.Sections.Add(new() { Key = "curriculum", Status = learning.Subjects.Count == 0 ? "empty" : "ready", Summary = "Published subjects and recorded coverage available through the verified student portal.",
			Metrics = [new("chapters", "Published chapters", learning.TotalChapters)],
			Series = [new("recorded-progress", learning.Subjects.Select(x => new AcademicInsightPointDto(x.Id.ToString(), x.Name, x.CoveragePercentage)).ToList())] });
		if (tasks != null) response.Sections.Add(new() { Key = "tasks", Status = tasks.TotalCount == 0 ? "empty" : "ready", Summary = "Verified personal learning tasks; due buckets cover the first 100 visible tasks.",
			Metrics = [new("tasks", "Visible tasks", tasks.TotalCount), new("pending", "Pending homework", overview?.PendingHomework)],
			Series = tasks.Items.GroupBy(x => x.Type).Select(g => new AcademicInsightSeriesDto(g.Key,
				g.GroupBy(x => x.DueDate.Date < MobileAcademicContextAppService.SchoolToday ? "overdue" : x.DueDate.Date == MobileAcademicContextAppService.SchoolToday ? "today" : "upcoming")
				.Select(bucket => new AcademicInsightPointDto(bucket.Key, bucket.Key, bucket.Count())).ToList())).ToList() });
		if (overview != null) response.Sections.Add(new() { Key = "exams", Status = overview.UpcomingExams.Count == 0 && scores.Count == 0 ? "empty" : "ready", Summary = resultPage == null ? "Upcoming exams only; the published results source could not be loaded." : "Upcoming exams and published results from the verified portal. Upcoming preview shows up to five exams; results preview includes up to 100.",
			Items = overview.UpcomingExams.Select(x => new AcademicInsightItemDto(x.Id.ToString(), x.Title, Status: x.Status)).ToList(),
			Series = [new("stages", resultPage?.Items.GroupBy(x => x.Status).Select(g => new AcademicInsightPointDto(g.Key, g.Key, g.Count())).ToList() ?? [])] });
		if (markers != null) response.Sections.Add(AcademicInsightsCalendarPreview.Personal(markers, DateOnly.FromDateTime(today)));
		if (overview == null)
			response.Sections.Add(new() { Key = "exams", Status = "error", Summary = "This section could not be loaded. Try again." });
		var from = DateOnly.FromDateTime(MobileAcademicContextAppService.SchoolToday);
		var yearStart = DateOnly.FromDateTime(placement.Year.StartDate); var yearEnd = DateOnly.FromDateTime(placement.Year.EndDate);
		if (from > yearEnd) from = yearEnd.AddDays(-6);
		if (from < yearStart) from = yearStart;
		var to = from.AddDays(6); if (to > yearEnd) to = yearEnd;
		var schedule = await Load("timetable", () => timetables.GetScheduleAsync(new() { StudentId = selected, AcademicYearId = input.AcademicYearId, FromDate = from, ToDate = to }));
		if (schedule == null) return response;
		var periods = schedule.Days.SelectMany(day => day.Periods.Where(period => period.PeriodNumber.HasValue).Select(period =>
			new AcademicInsightPointDto(period.PeriodId.ToString(), period.Subject?.Name ?? period.Title ?? "Period", 1, (int)day.Date.DayOfWeek, period.PeriodNumber, period.Status))).ToList();
		response.Sections.Add(new() { Key = "timetable", Status = schedule.Availability == "available" ? periods.Count == 0 ? "empty" : "ready" : "unavailable",
			Summary = $"Verified personal timetable from {from:yyyy-MM-dd} to {to:yyyy-MM-dd}. Only numbered periods appear in the matrix.",
			Series = [new("periods", periods)] });
		return response;
	}
}
