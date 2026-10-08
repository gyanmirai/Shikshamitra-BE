using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.Records;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students;

namespace ShikshaMitra.Students.Dashboard;

[RemoteService(false)]
public class StudentModuleDashboardAttendanceReader(IAttendanceDayRepository days, StudentScopeAccessService scope,
	IRepository<Division, Guid> divisions, IRepository<StudentLeave, Guid> leaves, IStudentRepository students,
	StudentRecordsEnrollmentScopeService enrollmentScope) : ApplicationService
{
	public async Task FillAsync(StudentModuleDashboardSectionDto result, StudentModuleDashboardContextDto context)
	{
		result.Description = "Saved registers in this academic year. Drafts still need submission; missing registers and policy-based percentages are available in Attendance.";
		if (context.Links.Any(link => link.Id == "attendance"))
		{
			await AuthorizationService.CheckAsync(P.Attendance_MarkAttendance.View);
			var allowedDivisions = await scope.ApplyCurrentUserDivisionScopeAsync(await divisions.GetQueryableAsync(), result.AcademicYearId);
			var query = (await days.GetQueryableAsync()).Where(day => day.TenantId == CurrentTenant.Id && day.AcademicYearId == result.AcademicYearId &&
				allowedDivisions.Any(d => d.Id == day.DivisionId && d.StandardId == day.StandardId));
			var counts = await AsyncExecuter.ToListAsync(query.GroupBy(day => day.IsSubmitted).Select(group => new { Submitted = group.Key, Count = group.Count() }));
			Add(result, "submitted", "Submitted registers", counts.FirstOrDefault(row => row.Submitted)?.Count ?? 0, P.Attendance_MarkAttendance.View, "/student/attendance/take-attendance", "success");
			Add(result, "drafts", "Draft registers", counts.FirstOrDefault(row => !row.Submitted)?.Count ?? 0, P.Attendance_MarkAttendance.View, "/student/attendance/take-attendance", "warning");
		}
		if (context.Links.Any(link => link.Id == "leave"))
		{
			await AuthorizationService.CheckAsync(P.Attendance_LeaveAndAbsent.View);
			var studentQuery = await scope.ApplyCurrentUserScopeAsync(await students.GetQueryableWithoutAutoIncludesAsync());
			var enrollments = (await enrollmentScope.GetQueryableAsync()).Where(e => e.AcademicYearId == result.AcademicYearId);
			var count = await AsyncExecuter.CountAsync((await leaves.GetQueryableAsync()).Where(leave => leave.TenantId == CurrentTenant.Id &&
				leave.Status == StudentLeaveStatusEnum.Pending && studentQuery.Any(s => s.Id == leave.StudentId) && enrollments.Any(e => e.StudentId == leave.StudentId)));
			Add(result, "leave", "Pending leave requests", count, P.Attendance_LeaveAndAbsent.View, "/student/attendance/leave-absent", "warning");
		}
	}

	private static void Add(StudentModuleDashboardSectionDto result, string id, string label, int value, string permission, string href, string tone) =>
		result.Metrics.Add(new() { Id = id, Label = label, Value = value, Permission = permission, Href = href, Tone = value == 0 ? "neutral" : tone });
}
