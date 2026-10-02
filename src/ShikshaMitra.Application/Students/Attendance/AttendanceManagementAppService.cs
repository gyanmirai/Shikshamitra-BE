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
public class AttendanceManagementAppService(AttendanceRosterService rosters, AttendanceReportService reports,
	AttendanceCorrectionService corrections, IRepository<StudentEnrollment, Guid> enrollments,
	IStudentRepository students, StudentScopeAccessService scope, AttendanceRegisterQueryService register) : ApplicationService, IAttendanceManagementAppService
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

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.View)]
	public async Task<PagedResultDto<AttendanceEnrollmentDto>> GetEnrollmentsAsync(AttendanceRegisterInput input)
	{
		if (!input.StandardId.HasValue || !input.DivisionId.HasValue) throw new BusinessException(AttendanceOperationErrorCodes.PlacementRequired);
		await scope.EnsureCanAccessAttendanceScopeAsync(input.AcademicYearId, input.StandardId.Value, input.DivisionId.Value, "attendance enrollment configuration");
		var query = (await enrollments.GetQueryableAsync()).Where(e => e.TenantId == CurrentTenant.Id && e.AcademicYearId == input.AcademicYearId &&
			e.StandardId == input.StandardId && e.DivisionId == input.DivisionId);
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(e => e.RollNumber).ThenBy(e => e.Id).Skip(input.SkipCount).Take(input.MaxResultCount));
		var ids = rows.Select(e => e.StudentId).ToArray();
		var identities = await AsyncExecuter.ToListAsync((await students.GetQueryableWithoutAutoIncludesAsync()).Where(s => s.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, s.Id)));
		return new(count, rows.Select(e => MapEnrollment(e, identities.Single(s => s.Id == e.StudentId))).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.Add)]
	[UnitOfWork(isTransactional: true)]
	public async Task<AttendanceEnrollmentDto> ConfirmStartAsync(Guid enrollmentId, ConfirmAttendanceStartInput input)
	{
		var enrollment = await enrollments.GetAsync(e => e.Id == enrollmentId && e.TenantId == CurrentTenant.Id);
		var student = await scope.GetStudentAsync(enrollment.StudentId, "confirm attendance start");
		if (!enrollment.StandardId.HasValue || !enrollment.DivisionId.HasValue || !enrollment.IsActive) throw new BusinessException(AttendanceOperationErrorCodes.PlacementRequired);
		await rosters.LoadAsync(enrollment.AcademicYearId, enrollment.StandardId.Value, enrollment.DivisionId.Value, input.Date);
		if (enrollment.ConcurrencyStamp != input.Revision) throw new BusinessException(AttendanceOperationErrorCodes.ConcurrencyConflict);
		enrollment.ConfirmAttendanceStart(input.Date); await enrollments.UpdateAsync(enrollment, autoSave: true);
		return MapEnrollment(enrollment, student);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.View)]
	public Task<PagedResultDto<AttendanceCorrectionDto>> GetCorrectionsAsync(AttendanceCorrectionListInput input) => corrections.ListAsync(input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.Add)]
	public Task<AttendanceCorrectionDto> RequestCorrectionAsync(AttendanceCorrectionInput input) => corrections.RequestAsync(input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.Add)]
	public Task<AttendanceCorrectionDto> ApproveCorrectionAsync(Guid id, AttendanceCorrectionDecisionInput input) => corrections.DecideAsync(id, input, approve: true);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.Add)]
	public Task<AttendanceCorrectionDto> RejectCorrectionAsync(Guid id, AttendanceCorrectionDecisionInput input) => corrections.DecideAsync(id, input, approve: false);

	private static AttendanceEnrollmentDto MapEnrollment(StudentEnrollment e, Student student) => new() { EnrollmentId = e.Id, StudentId = e.StudentId,
		FullName = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))),
		RollNo = e.RollNumber, AttendanceStartDate = e.AttendanceStartDate, Revision = e.ConcurrencyStamp };
}
