using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.Mobile;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Finance.Mobile;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Students.Portal;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Dashboard;

[RemoteService(false)]
public class DashboardPersonalFocusReader(StudentPortalAccessService portal, MobileAcademicContextAppService academic,
	IMobileAttendanceAppService attendance, IRepository<ExamResultHeader, Guid> headers,
	IRepository<ExamResultDetail, Guid> details, IRepository<Exam, Guid> exams,
	IMobileHomeworkAppService homework, IMobileExamAppService mobileExams,
	IRepository<HomeworkAssignmentEntity, Guid> homeworkRows, IRepository<AssignmentEntity, Guid> assignmentRows,
	IMobileFinanceReadService finance, MobileFinanceAccessService financeAccess, IDueFeeRepository dues, IRepository<FeeHead, Guid> feeHeads) : ApplicationService
{
	public async Task<DashboardSummaryDto> ReadAsync(DashboardSummaryRequestDto input, DashboardDateRangeDto range, DateTime now)
	{
		var links = await portal.GetMyStudentsAsync();
		if (links.Count == 0) throw new AbpAuthorizationException();
		var students = links.GroupBy(x => x.StudentId).Select(g => new DashboardStudentOptionDto(g.Key, g.First().StudentName)).OrderBy(x => x.Label).ThenBy(x => x.Id).ToList();
		if (input.StudentId.HasValue && !students.Any(x => x.Id == input.StudentId)) throw new AbpAuthorizationException();
		var selected = input.StudentId ?? (students.Count == 1 ? students[0].Id : (Guid?)null);
		var result = new DashboardSummaryDto { DashboardTemplate = "studentParent", Students = students, SelectedStudentId = selected,
			PresentationRole = links.Any(x => (!selected.HasValue || x.StudentId == selected) && x.AccountType == "guardian") ? "parent" : "student",
			AcademicYearId = input.AcademicYearId, DateRange = range, GeneratedAtUtc = DateTime.SpecifyKind(now, DateTimeKind.Utc),
			ScopeLabel = selected.HasValue ? students.Single(x => x.Id == selected).Label : "Select a linked student" };
		var focus = new DashboardFocusDto { Kind = "results", WidgetId = "student-academic-summary", AccessMode = "personal", Unit = "percent",
			Title = "Published exam results", Description = "Published results for the verified linked student. Withheld and unpublished results are excluded." };
		result.Focus = focus;
		if (!selected.HasValue) { focus.Message = "Select a linked student to load their overview."; return result; }
		Guid? year = null;
		if (!string.IsNullOrWhiteSpace(input.AcademicYearId))
		{
			if (!Guid.TryParse(input.AcademicYearId, out var parsed) || parsed == Guid.Empty) throw MobileAcademicContextAppService.Invalid("Select a valid academic year.");
			year = parsed;
		}
		var placement = await academic.ResolveAsync(new() { StudentId = selected, AcademicYearId = year });
		result.AcademicYearId = placement.Year.Id.ToString();
		result.ScopeLabel = $"{students.Single(x => x.Id == selected).Label} / {placement.StandardName} / {placement.DivisionName}";
		focus.PeriodLabel = placement.Year.Name;
		try
		{
			var query = from detail in await details.GetQueryableAsync()
				join header in await headers.GetQueryableAsync() on detail.ResultHeaderId equals header.Id
				join exam in await exams.GetQueryableAsync() on header.ExamId equals exam.Id
				where detail.TenantId == CurrentTenant.Id && header.TenantId == CurrentTenant.Id && exam.TenantId == CurrentTenant.Id &&
					detail.StudentId == selected && detail.DivisionId == placement.DivisionId && header.DivisionId == placement.DivisionId &&
					exam.StandardId == placement.StandardId && detail.ExamId == exam.Id &&
					(exam.Status == ExamStatus.Published || exam.Status == ExamStatus.Completed) &&
					(header.Status == ExamResultStatus.Published || header.Status == ExamResultStatus.Frozen) && detail.ResultStatus != ExamStudentResultStatus.Withheld
				orderby header.PublishedOn descending, header.Id descending
				select new { header.Id, exam.ExamName, header.PublishedOn, detail.Percentage, detail.TotalMaxMarks, detail.Grade, detail.ResultStatus };
			var rows = await AsyncExecuter.ToListAsync(query.Take(6));
			focus.Status = rows.Count == 0 ? "empty" : "ready";
			focus.Description += " Showing up to six latest results.";
			focus.Series = [new("results", "Latest published results", rows.AsEnumerable().Reverse().Select(x => new DashboardFocusPointDto(x.Id.ToString(), x.ExamName, x.TotalMaxMarks > 0 ? (decimal)x.Percentage : null)).ToList())];
			focus.Items = rows.Select(x => new DashboardFocusItemDto { Id = x.Id.ToString(), Title = x.ExamName, Subtitle = x.Grade,
				Date = x.PublishedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Status = x.ResultStatus.ToString() }).ToList();
			result.ModuleSnapshots.Add(new() { Id = "student-academic-summary", Title = "Published results", Count = rows.Count,
				Subtitle = "Up to six latest published results", Kind = "health", Status = "info" });
			if (rows.Count > 0 && rows[0].TotalMaxMarks > 0)
				result.KpiCards.Add(new() { Id = "student-latest-result", Title = "Latest published score", Value = $"{rows[0].Percentage:0.##}%",
					Subtitle = rows[0].ExamName, Kind = "metric", Status = "info" });
		}
		catch (Exception error)
		{
			Logger.LogWarning(error, "Personal dashboard results could not be loaded.");
			focus.Status = "error"; focus.Message = "Published results could not be loaded. Try again.";
			result.ZoneErrors["focus"] = focus.Message; result.DataState = "partial";
		}
		try
		{
			var overview = await attendance.GetOverviewAsync(new() { StudentId = selected, AcademicYearId = placement.Year.Id });
			focus.Metrics.Add(new("attendance", "Academic year attendance", overview.CurrentPercentage.HasValue ? (decimal)overview.CurrentPercentage.Value : null, "percent"));
			result.KpiCards.Add(new() { Id = "student-attendance", Title = "Attendance", Value = overview.CurrentPercentage.HasValue ? $"{overview.CurrentPercentage:0.##}%" : null,
				Subtitle = "Verified linked student / enrolled academic year", Kind = "metric", Status = "info" });
		}
		catch (Exception error)
		{
			Logger.LogWarning(error, "Personal dashboard attendance could not be loaded.");
			result.ZoneErrors["personalAttendance"] = "Personal attendance could not be loaded."; result.DataState = "partial";
		}
		await FillWorkAsync(result, placement);
		return result;
	}

	private async Task FillWorkAsync(DashboardSummaryDto result, MobilePlacement placement)
	{
		try
		{
			var workCount = await homeworkRows.CountAsync(x => x.TenantId == CurrentTenant.Id && x.StandardId == placement.StandardId && (!x.AcademicYearId.HasValue || x.AcademicYearId == placement.Year.Id));
			workCount += await assignmentRows.CountAsync(x => x.TenantId == CurrentTenant.Id && x.StandardId == placement.StandardId && (!x.AcademicYearId.HasValue || x.AcademicYearId == placement.Year.Id));
			if (workCount <= 200)
			{
				var pending = await homework.GetListAsync(new() { StudentId = placement.Student.Id, AcademicYearId = placement.Year.Id, Tab = "pending", PageSize = 5 });
				result.KpiCards.Add(new() { Id = "student-assignments", Title = "Pending learning tasks", Count = pending.TotalCount, Value = pending.TotalCount,
					Subtitle = "Published tasks for the linked student", Kind = "metric", Status = "info" });
				result.ActionItems.AddRange(pending.Items.Take(1).Select(x => new DashboardWidgetDto { Id = "student-learning-tasks", Title = x.Title,
					Subtitle = x.SubjectName, DueLabel = x.DueDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), Status = x.Status, Kind = "action" }));
			}
			else result.KpiCards.Add(new() { Id = "student-assignments", Title = "Pending learning tasks", Value = null, Subtitle = "Open Learning Tasks for the full workload.", Status = "info", Kind = "metric" });
			if (await exams.CountAsync(x => x.TenantId == CurrentTenant.Id && x.StandardId == placement.StandardId) <= 100)
			{
				var upcoming = await mobileExams.GetListAsync(new() { StudentId = placement.Student.Id, AcademicYearId = placement.Year.Id, Status = "upcoming", PageSize = 5 });
				result.KpiCards.Add(new() { Id = "student-upcoming-exams", Title = "Upcoming exams", Count = upcoming.TotalCount, Value = upcoming.TotalCount, Kind = "metric", Status = "info" });
				result.CalendarItems.AddRange(upcoming.Items.Take(1).Select(x => new DashboardWidgetDto { Id = "student-upcoming-exam", Title = x.Title,
					DueLabel = x.Schedule.FirstOrDefault()?.ExamDate?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), Status = "info", Kind = "timeline" }));
			}
			if (result.PresentationRole == "parent" && await financeAccess.IsGrantedAsync(MobileFinancePermissions.View))
			{
				var heads = (await feeHeads.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == placement.Year.Id);
				var count = await AsyncExecuter.CountAsync((await dues.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.StudentId == placement.Student.Id && heads.Any(h => h.Id == x.FeeHeadId)));
				if (count <= 500)
				{
					var installments = await finance.InstallmentsAsync(placement.Student.Id, placement.Year.Id);
					var next = installments.Where(x => decimal.Parse(x.Due, CultureInfo.InvariantCulture) > 0).OrderBy(x => x.DueDate).ThenBy(x => x.DueFeeId).FirstOrDefault();
					result.KpiCards.Add(new() { Id = "student-fee-status", Title = "Next fee due", Value = next == null ? null : $"INR {next.Due}",
						Subtitle = next == null ? "No outstanding installment in the selected academic year" : $"{next.FeeHeadName} · {next.DueDate:dd MMM yyyy}", Kind = "metric", Status = "info" });
				}
			}
		}
		catch (Exception error)
		{
			Logger.LogWarning(error, "Personal dashboard workload could not be loaded.");
			result.ZoneErrors["personalWork"] = "Some personal tasks, exams or fees could not be loaded.";
			result.DataState = "partial";
		}
	}
}
