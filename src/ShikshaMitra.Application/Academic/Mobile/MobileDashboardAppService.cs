using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Mobile;

[Authorize]
[RemoteService(false)]
public class MobileDashboardAppService(MobileAcademicContextAppService context, IMobileHomeworkAppService homework,
	IMobileAttendanceAppService attendance, IMobileExamAppService exams, IMobileCalendarAppService calendar)
	: ApplicationService, IMobileDashboardAppService
{
	public async Task<MobileDashboardDto> GetAsync(MobileAcademicInput input)
	{
		var p = await context.ResolveAsync(input);
		var resolved = new MobileAcademicInput { StudentId = p.Student.Id, AcademicYearId = p.Year.Id };
		var pending = await homework.GetListAsync(new() { StudentId = p.Student.Id, AcademicYearId = p.Year.Id, Tab = "pending", PageSize = 1 });
		var overview = await attendance.GetOverviewAsync(resolved);
		var upcoming = await exams.GetListAsync(new() { StudentId = p.Student.Id, AcademicYearId = p.Year.Id, Status = "upcoming", PageSize = 5 });
		var today = MobileAcademicContextAppService.SchoolToday;
		var markers = await calendar.GetAsync(new() { StudentId = p.Student.Id, AcademicYearId = p.Year.Id, Year = today.Year, Month = today.Month });
		return new(p.Student.Id, p.Year.Id, pending.TotalCount, overview, upcoming.Items, markers.Where(x => x.Date >= DateOnly.FromDateTime(today)).Take(5).ToList());
	}
}
