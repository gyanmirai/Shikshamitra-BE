using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Dashboard;

[RemoteService(false)]
public class AcademicInsightsStudentReader(IStudentRepository students, StudentScopeAccessService studentScope,
	IAttendanceDayRepository attendanceDays, IRepository<AttendanceEntry, Guid> attendanceEntries,
	IRepository<ExamResultDetail, Guid> results, IRepository<ExamResultHeader, Guid> headers,
	IRepository<Exam, Guid> exams) : ApplicationService
{
	public async Task<AcademicInsightSectionDto> ReadAsync(AcademicInsightsContext c)
	{
		var divisions = c.DivisionIds; var standards = c.StandardIds;
		var pupils = (await studentScope.ApplyCurrentUserScopeAsync(await students.GetQueryableWithoutAutoIncludesAsync())).Where(x => x.TenantId == c.TenantId &&
			x.AcademicYearId == c.Input.AcademicYearId && x.AcademicBoardId == c.Input.BoardId && x.AcademicMediumId == c.Input.EducationMediumId &&
			x.Status == StudentLifeCycleStatusEnum.Enrolled && x.DivisionId.HasValue && Enumerable.Contains(divisions, x.DivisionId.Value));
		if (c.Input.StudentId.HasValue)
		{
			pupils = pupils.Where(x => x.Id == c.Input.StudentId.Value);
			if (!await AsyncExecuter.AnyAsync(pupils)) throw new AbpAuthorizationException();
		}
		var total = await AsyncExecuter.CountAsync(pupils);
		var pupilIds = pupils.Select(x => x.Id);
		var today = Mobile.MobileAcademicContextAppService.SchoolToday; var start = today.AddDays(-30);
		var days = (await studentScope.ApplyCurrentUserAttendanceDayScopeAsync(await attendanceDays.GetQueryableAsync())).Where(x =>
			x.TenantId == c.TenantId && x.AcademicYearId == c.Input.AcademicYearId && Enumerable.Contains(divisions, x.DivisionId) && x.IsSubmitted && x.Date >= start && x.Date <= today);
		var attendance = from entry in await attendanceEntries.GetQueryableAsync()
			join day in days on entry.AttendanceDayId equals day.Id
			where entry.TenantId == c.TenantId && pupilIds.Contains(entry.StudentId) && !entry.ExcludedFromPercentage && entry.Status != AttendanceStatus.Pending
			group entry by entry.StudentId into g
			select new { StudentId = g.Key, Denominator = g.Sum(x => x.DenominatorCredit), Present = g.Sum(x => x.PresentCredit ?? (x.Status == AttendanceStatus.Present ? 1d : 0d)) };
		var published = (await headers.GetQueryableAsync()).Where(x => x.TenantId == c.TenantId && (x.Status == ExamResultStatus.Published || x.Status == ExamResultStatus.Frozen));
		var scores = from result in await results.GetQueryableAsync()
			join header in published on result.ResultHeaderId equals header.Id
			join exam in await exams.GetQueryableAsync() on result.ExamId equals exam.Id
			where result.TenantId == c.TenantId && exam.TenantId == c.TenantId && Enumerable.Contains(standards, exam.StandardId) &&
				Enumerable.Contains(divisions, result.DivisionId) && pupilIds.Contains(result.StudentId) && result.TotalMaxMarks > 0 && result.ResultStatus != ExamStudentResultStatus.Withheld
			group result by result.StudentId into g
			select new { StudentId = g.Key, Score = g.Average(x => x.Percentage) };
		var scatter = from pupil in pupils
			join a in attendance.Where(x => x.Denominator > 0) on pupil.Id equals a.StudentId
			join score in scores on pupil.Id equals score.StudentId
			orderby pupil.FirstName, pupil.LastName, pupil.Id
			select new { pupil.Id, Name = (pupil.FirstName ?? "") + " " + (pupil.LastName ?? ""), Attendance = a.Present * 100 / a.Denominator, score.Score };
		var matched = await AsyncExecuter.CountAsync(scatter);
		var points = await AsyncExecuter.ToListAsync(scatter.Take(60));
		return new() { Key = "student360", Status = total == 0 ? "empty" : "ready",
			Summary = "Submitted attendance in the last 30 days against average published exam scores. Preview shows up to 60 students with both denominators; totals cover the selected scope.",
			Metrics = [new("students", "Enrolled students", total), new("matched-students", "Students with attendance and scores", matched)],
			Series = [new("attendance-score", points.Select(x => new AcademicInsightPointDto(x.Id.ToString(), x.Name.Trim(), x.Score, Math.Round(x.Attendance, 2), Math.Round(x.Score, 2))).ToList())] };
	}
}
