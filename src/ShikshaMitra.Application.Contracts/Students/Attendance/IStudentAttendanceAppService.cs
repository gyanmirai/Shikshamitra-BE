using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using System;

namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// Application service interface for Student Attendance operations
/// </summary>
public interface IStudentAttendanceAppService : IApplicationService
{
	Task<AttendanceWorkspaceContextDto> GetWorkspaceContextAsync(Guid academicYearId);
	Task<PagedResultDto<StudentAttendanceDto>> GetWorkspaceAsync(AttendanceWorkspaceInput input);
	/// <summary>
	/// Gets the list of enrolled students for a specific date and division with their attendance status
	/// Permission: Students.Attendance.MarkAttendance.View
	/// </summary>
	Task<StudentAttendanceListDto> GetStudentsForDateAsync(GetStudentsForAttendanceInput input);

	/// <summary>
	/// Gets paged attendance history for a student and date range.
	/// Permission: Students.Attendance.MarkAttendance.View
	/// </summary>
	Task<PagedResultDto<StudentAttendanceHistoryDto>> GetAttendanceHistoryAsync(GetStudentAttendanceHistoryInput input);

	/// <summary>
	/// Marks or updates attendance for students
	/// Permission: Students.Attendance.MarkAttendance.Add
	/// </summary>
	Task<MarkAttendanceOutput> MarkAttendanceAsync(MarkAttendanceInput input);

	/// <summary>
	/// Locks an attendance day to prevent further modifications
	/// Permission: Students.Attendance.MarkAttendance.Delete (Lock)
	/// </summary>
	Task<LockAttendanceDayOutput> LockAttendanceDayAsync(LockAttendanceDayInput input);

	/// <summary>
	/// Unlocks an attendance day to allow modifications
	/// Permission: Students.Attendance.MarkAttendance.Delete (Unlock)
	/// </summary>
	Task<LockAttendanceDayOutput> UnlockAttendanceDayAsync(LockAttendanceDayInput input);
}
