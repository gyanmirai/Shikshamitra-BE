using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Attendance;

[Authorize]
[RemoteService(false)]
public class AttendanceManagementAppService(AttendanceReportService reports,
	AttendanceCorrectionService corrections, IRepository<StudentEnrollment, Guid> enrollments,
	StudentScopeAccessService scope, AttendanceRegisterQueryService register) : ApplicationService, IAttendanceManagementAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_Reports.View)]
	public Task<AttendanceRegisterDto> GetRegisterAsync(AttendanceRegisterInput input) => register.ReadAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View)]
	public async Task<AttendanceSummaryDto> GetSummaryAsync(AttendanceSummaryInput input)
	{
		await scope.GetStudentAsync(input.StudentId, "attendance summary");
		var member = await enrollments.GetAsync(e => e.TenantId == CurrentTenant.Id && e.StudentId == input.StudentId && e.AcademicYearId == input.AcademicYearId);
		var range = await reports.RangeAsync(input.AcademicYearId, input.StartDate, input.EndDate);
		return (await register.BuildRowsAsync(input.AcademicYearId, range.Start, range.End, [member], includeDaily: true)).SingleOrDefault()
			?? new() { StudentId = member.StudentId, EnrollmentId = member.Id, AcademicYearId = input.AcademicYearId, IsProvisional = true };
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.View)]
	public Task<PagedResultDto<AttendanceCorrectionDto>> GetCorrectionsAsync(AttendanceCorrectionListInput input) => corrections.ListAsync(input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.Add)]
	public Task<AttendanceCorrectionDto> RequestCorrectionAsync(AttendanceCorrectionInput input) => corrections.RequestAsync(input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.Add)]
	public Task<AttendanceCorrectionDto> ApproveCorrectionAsync(Guid id, AttendanceCorrectionDecisionInput input) => corrections.DecideAsync(id, input, approve: true);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.Add)]
	public Task<AttendanceCorrectionDto> RejectCorrectionAsync(Guid id, AttendanceCorrectionDecisionInput input) => corrections.DecideAsync(id, input, approve: false);

}
