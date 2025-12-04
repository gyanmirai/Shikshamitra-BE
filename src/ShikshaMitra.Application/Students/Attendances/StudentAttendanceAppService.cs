using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Attendance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Attendances;

/// <summary>
/// Application service for Student Attendance operations
/// </summary>
[Authorize]
[RemoteService(false)]
public class StudentAttendanceAppService : ApplicationService, IStudentAttendanceAppService
{
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IAttendanceDayRepository _attendanceDayRepository;
	private readonly AttendanceManager _attendanceManager;
	private readonly IRepository<TenantSettings.AcademicYears.AcademicYear, Guid> _academicYearRepository;
	private readonly IRepository<TenantSettings.Standards.Standard, Guid> _standardRepository;
	private readonly IRepository<TenantSettings.Divisions.Division, Guid> _divisionRepository;

	public StudentAttendanceAppService(
		IRepository<Student, Guid> studentRepository,
		IAttendanceDayRepository attendanceDayRepository,
		IAttendanceEntryRepository attendanceEntryRepository,
		AttendanceManager attendanceManager,
		IRepository<TenantSettings.AcademicYears.AcademicYear, Guid> academicYearRepository,
		IRepository<TenantSettings.Standards.Standard, Guid> standardRepository,
		IRepository<TenantSettings.Divisions.Division, Guid> divisionRepository)
	{
		_studentRepository = studentRepository;
		_attendanceDayRepository = attendanceDayRepository;
		_attendanceManager = attendanceManager;
		_academicYearRepository = academicYearRepository;
		_standardRepository = standardRepository;
		_divisionRepository = divisionRepository;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View)]
	public async Task<StudentAttendanceListDto> GetStudentsForDateAsync(GetStudentsForAttendanceInput input)
	{
		// Validate inputs
		await ValidateInputsAsync(input.AcademicYearId, input.StandardId, input.DivisionId);

		// Get students enrolled in the specified Standard and Division
		var students = await _studentRepository.GetListAsync(s =>
			s.StandardId == input.StandardId &&
			s.DivisionId == input.DivisionId);

		// Get existing attendance day if any
		var attendanceDay = await _attendanceDayRepository.FindByDateAndDivisionAsync(
			input.AcademicYearId,
			input.StandardId,
			input.DivisionId,
			input.Date,
			includeEntries: true);

		// Build the response
		var studentDtos = new List<StudentAttendanceDto>();

		foreach (var student in students.OrderBy(s => s.RollNo).ThenBy(s => s.FirstName))
		{
			var attendanceEntry = attendanceDay?.Entries.FirstOrDefault(e => e.StudentId == student.Id);

			studentDtos.Add(new StudentAttendanceDto
			{
				StudentId = student.Id,
				GrNo = student.GrNo,
				RollNo = student.RollNo,
				FirstName = student.FirstName,
				MiddleName = student.MiddleName,
				LastName = student.LastName,
				Status = attendanceEntry?.Status ?? AttendanceStatus.Pending,
				Remarks = attendanceEntry?.Remarks
			});
		}

		var presentCount = studentDtos.Count(s => s.Status == AttendanceStatus.Present);
		var absentCount = studentDtos.Count(s => s.Status == AttendanceStatus.Absent);
		var pendingCount = studentDtos.Count(s => s.Status == AttendanceStatus.Pending);

		return new StudentAttendanceListDto
		{
			AttendanceDayId = attendanceDay?.Id,
			Date = input.Date.Date,
			IsLocked = attendanceDay?.IsLocked ?? false,
			TotalStudents = studentDtos.Count,
			PresentCount = presentCount,
			AbsentCount = absentCount,
			PendingCount = pendingCount,
			Students = studentDtos
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Add)]
	public async Task<MarkAttendanceOutput> MarkAttendanceAsync(MarkAttendanceInput input)
	{
		// Validate inputs
		await ValidateInputsAsync(input.AcademicYearId, input.StandardId, input.DivisionId);

		// Validate date is not in the future
		if (input.Date.Date > DateTime.Today)
		{
			throw new BusinessException("SM:CannotMarkFutureAttendance")
				.WithData("Date", input.Date);
		}

		// Get or create attendance day
		var attendanceDay = await _attendanceManager.GetOrCreateAttendanceDayAsync(
			input.AcademicYearId,
			input.StandardId,
			input.DivisionId,
			input.Date);

		// Validate students exist
		var studentIds = input.Entries.Select(e => e.StudentId).ToList();
		await _attendanceManager.ValidateStudentsAsync(studentIds);

		// Mark attendance
		var attendanceEntries = input.Entries.Select(e => new AttendanceManager.AttendanceEntryInput
		{
			StudentId = e.StudentId,
			Status = e.Status,
			Remarks = e.Remarks
		}).ToList();

		await _attendanceManager.MarkAttendanceAsync(attendanceDay.Id, attendanceEntries);

		// Calculate counts
		var presentCount = input.Entries.Count(e => e.Status == AttendanceStatus.Present);
		var absentCount = input.Entries.Count(e => e.Status == AttendanceStatus.Absent);

		return new MarkAttendanceOutput
		{
			AttendanceDayId = attendanceDay.Id,
			IsLocked = attendanceDay.IsLocked,
			TotalMarked = input.Entries.Count,
			PresentCount = presentCount,
			AbsentCount = absentCount
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Delete)]
	public async Task<LockAttendanceDayOutput> LockAttendanceDayAsync(LockAttendanceDayInput input)
	{
		// Validate inputs
		await ValidateInputsAsync(input.AcademicYearId, input.StandardId, input.DivisionId);

		// Find attendance day
		var attendanceDay = await _attendanceDayRepository.FindByDateAndDivisionAsync(
			input.AcademicYearId,
			input.StandardId,
			input.DivisionId,
			input.Date);

		if (attendanceDay == null)
		{
			throw new BusinessException("SM:AttendanceDayNotFound")
				.WithData("Date", input.Date)
				.WithData("StandardId", input.StandardId)
				.WithData("DivisionId", input.DivisionId);
		}

		// Lock the day
		await _attendanceManager.LockAttendanceDayAsync(attendanceDay.Id);

		return new LockAttendanceDayOutput
		{
			AttendanceDayId = attendanceDay.Id,
			IsLocked = true,
			Message = "Attendance day locked successfully"
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Delete)]
	public async Task<LockAttendanceDayOutput> UnlockAttendanceDayAsync(LockAttendanceDayInput input)
	{
		// Validate inputs
		await ValidateInputsAsync(input.AcademicYearId, input.StandardId, input.DivisionId);

		// Find attendance day
		var attendanceDay = await _attendanceDayRepository.FindByDateAndDivisionAsync(
			input.AcademicYearId,
			input.StandardId,
			input.DivisionId,
			input.Date);

		if (attendanceDay == null)
		{
			throw new BusinessException("SM:AttendanceDayNotFound")
				.WithData("Date", input.Date)
				.WithData("StandardId", input.StandardId)
				.WithData("DivisionId", input.DivisionId);
		}

		// Unlock the day
		await _attendanceManager.UnlockAttendanceDayAsync(attendanceDay.Id);

		return new LockAttendanceDayOutput
		{
			AttendanceDayId = attendanceDay.Id,
			IsLocked = false,
			Message = "Attendance day unlocked successfully"
		};
	}

	#region Private Helper Methods

	private async Task ValidateInputsAsync(Guid academicYearId, Guid standardId, Guid divisionId)
	{
		// Validate Academic Year exists
		if (!await _academicYearRepository.AnyAsync(ay => ay.Id == academicYearId))
		{
			throw new BusinessException("SM:AcademicYearNotFound")
				.WithData("AcademicYearId", academicYearId);
		}

		// Validate Standard exists
		if (!await _standardRepository.AnyAsync(s => s.Id == standardId))
		{
			throw new BusinessException("SM:StandardNotFound")
				.WithData("StandardId", standardId);
		}

		// Validate Division exists and belongs to the Standard
		var division = await _divisionRepository.FirstOrDefaultAsync(d => d.Id == divisionId);
		if (division == null)
		{
			throw new BusinessException("SM:DivisionNotFound")
				.WithData("DivisionId", divisionId);
		}

		if (division.StandardId != standardId)
		{
			throw new BusinessException("SM:DivisionDoesNotBelongToStandard")
				.WithData("DivisionId", divisionId)
				.WithData("StandardId", standardId);
		}
	}

	#endregion
}
