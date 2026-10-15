using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students;

namespace ShikshaMitra.Students.Dashboard;

public partial class StudentModuleDashboardAdminReader
{
	private async Task FillAttendanceAsync(StudentModuleDashboardSectionDto result, StudentModuleDashboardContextDto context, StudentModuleDashboardFilter filter)
	{
		result.Description = "Today's expected registers and pending leave in the selected academic scope.";
		if (context.Links.Any(l => l.Id == "attendance")) result.AttendanceToday = (await EvaluateRegistersAsync(filter)).Summary;
		if (context.Links.Any(l => l.Id == "leave"))
		{
			var count = await LazyServiceProvider.LazyGetRequiredService<StudentLeaveAppService>().GetStatusCountsAsync(new GetStudentLeaveListInput {
				AcademicYearId = filter.YearId, BoardId = filter.BoardId, EducationMediumId = filter.MediumId, StandardId = filter.StandardId, DivisionId = filter.DivisionId });
			var metric = Metric("leave", "Pending leave requests", checked((int)count.PendingCount), filter.Link("/student/attendance/leave-absent", "pending"), P.Attendance_LeaveAndAbsent.View, "warning");
			result.WorkGroups.Add(metric); result.Metrics.Add(metric);
		}
	}

	public async Task<PagedResultDto<StudentModuleDashboardRegisterDto>> RegistersAsync(StudentModuleDashboardFilter filter, int skipCount, int maxResultCount)
	{
		if (skipCount < 0 || maxResultCount is < 1 or > 100) throw new BusinessException("Students:Records:InvalidDirectoryQuery");
		var evaluated = await EvaluateRegistersAsync(filter);
		var pending = evaluated.Registers.Where(r => r.Status != "submitted").OrderBy(r => r.Status == "review" ? 0 : r.Status == "missing" ? 1 : 2)
			.ThenBy(r => r.ClassName).ThenBy(r => r.DivisionName).ThenBy(r => r.DivisionId).ToList();
		return new(pending.Count, pending.Skip(skipCount).Take(maxResultCount).ToList());
	}

	private async Task<(StudentModuleDashboardAttendanceTodayDto Summary, List<StudentModuleDashboardRegisterDto> Registers)> EvaluateRegistersAsync(StudentModuleDashboardFilter filter)
	{
		var policy = await LazyServiceProvider.LazyGetRequiredService<AttendancePolicyResolver>().ResolveAsync(filter.YearId, Clock.Now.Date);
		var today = AttendanceSchoolDate.Today(policy.Options, Clock.Now);
		var summary = new StudentModuleDashboardAttendanceTodayDto { SchoolDate = today, TimeZone = policy.Options.SchoolTimeZone,
			Href = filter.Link("/student/attendance/take-attendance", "pending-registers") };
		var year = await LazyServiceProvider.LazyGetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(y => y.Id == filter.YearId && y.TenantId == CurrentTenant.Id);
		var registers = new List<StudentModuleDashboardRegisterDto>();
		if (today < year.StartDate.Date || today > year.EndDate.Date) { summary.Availability = "outsideYear"; return (summary, registers); }
		var hierarchy = await options.GetAsync(filter.YearId);
		var mediumIds = hierarchy.Mediums.Where(m => !filter.BoardId.HasValue || m.ParentId == filter.BoardId).Select(m => m.Id).ToHashSet();
		var standards = hierarchy.Standards.Where(s => mediumIds.Contains(s.ParentId!.Value) && (!filter.MediumId.HasValue || s.ParentId == filter.MediumId) &&
			(!filter.StandardId.HasValue || s.Id == filter.StandardId)).ToDictionary(s => s.Id);
		var divisions = hierarchy.Divisions.Where(d => d.ParentId.HasValue && standards.ContainsKey(d.ParentId.Value) && (!filter.DivisionId.HasValue || d.Id == filter.DivisionId));
		var divisionList = divisions.ToDictionary(d => d.Id);
		var states = await LazyServiceProvider.LazyGetRequiredService<AttendanceDailyRegisterProjection>().ReadAsync(filter.YearId,
			divisionList.Values.Select(d => (d.ParentId!.Value, d.Id)).ToArray(), today);
		foreach (var state in states)
		{
			summary.Provisional |= state.Provisional;
			if (state.ExpectedStudents == 0) continue;
			registers.Add(new() { StandardId = state.StandardId, DivisionId = state.DivisionId, ClassName = standards[state.StandardId].Name,
				DivisionName = divisionList[state.DivisionId].Name, Date = today, Status = state.Status,
				Href = (filter with { StandardId = state.StandardId, DivisionId = state.DivisionId }).Link($"/student/attendance/take-attendance?date={today:yyyy-MM-dd}") });
		}
		summary.Expected = registers.Count; summary.Missing = registers.Count(r => r.Status == "missing"); summary.Draft = registers.Count(r => r.Status == "draft");
		summary.Submitted = registers.Count(r => r.Status is "submitted" or "review"); summary.Review = registers.Count(r => r.Status == "review");
		if (registers.Count == 0) summary.Availability = "noExpected";
		return (summary, registers);
	}
}
