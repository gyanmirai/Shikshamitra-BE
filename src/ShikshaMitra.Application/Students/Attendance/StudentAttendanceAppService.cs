using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Attendance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// Application service for Student Attendance operations
/// </summary>
[Authorize]
[RemoteService(false)]
public class StudentAttendanceAppService : ApplicationService, IStudentAttendanceAppService
{
	private readonly IStudentRepository _studentRepository;
	private readonly IAttendanceDayRepository _attendanceDayRepository;
	private readonly IAttendanceEntryRepository _attendanceEntryRepository;
	private readonly AttendanceManager _attendanceManager;
	private readonly IRepository<TenantSettings.AcademicYears.AcademicYear, Guid> _academicYearRepository;
	private readonly IRepository<TenantSettings.Standards.Standard, Guid> _standardRepository;
	private readonly IRepository<TenantSettings.Divisions.Division, Guid> _divisionRepository;
	private readonly IRepository<TenantSettings.EducationMediums.EducationMedium, Guid> _educationMediumRepository;
	private readonly StudentScopeAccessService _studentScopeAccessService;

	public StudentAttendanceAppService(
		IStudentRepository studentRepository,
		IAttendanceDayRepository attendanceDayRepository,
		IAttendanceEntryRepository attendanceEntryRepository,
		AttendanceManager attendanceManager,
		IRepository<TenantSettings.AcademicYears.AcademicYear, Guid> academicYearRepository,
		IRepository<TenantSettings.Standards.Standard, Guid> standardRepository,
		IRepository<TenantSettings.Divisions.Division, Guid> divisionRepository,
		IRepository<TenantSettings.EducationMediums.EducationMedium, Guid> educationMediumRepository,
		StudentScopeAccessService studentScopeAccessService)
	{
		_studentRepository = studentRepository;
		_attendanceDayRepository = attendanceDayRepository;
		_attendanceEntryRepository = attendanceEntryRepository;
		_attendanceManager = attendanceManager;
		_academicYearRepository = academicYearRepository;
		_standardRepository = standardRepository;
		_divisionRepository = divisionRepository;
		_educationMediumRepository = educationMediumRepository;
		_studentScopeAccessService = studentScopeAccessService;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View)]
	public async Task<StudentAttendanceListDto> GetStudentsForDateAsync(GetStudentsForAttendanceInput input)
	{
		// Validate inputs
		await ValidateInputsAsync(input.AcademicYearId, input.StandardId, input.DivisionId);

		var studentQuery = (await _studentRepository.GetQueryableWithoutAutoIncludesAsync())
			.Where(s => s.StandardId == input.StandardId && s.DivisionId == input.DivisionId);
		studentQuery = await _studentScopeAccessService.ApplyCurrentUserScopeAsync(studentQuery);
		var students = await AsyncExecuter.ToListAsync(studentQuery);

		var standard = await _standardRepository.GetAsync(input.StandardId);
		var division = await _divisionRepository.GetAsync(input.DivisionId);
		var educationMediumIds = students.Select(s => s.AcademicMediumId).Distinct().ToList();
		var educationMediums = await _educationMediumRepository.GetListAsync(m => educationMediumIds.Contains(m.Id));
		var educationMediumNames = educationMediums.ToDictionary(m => m.Id, m => m.Name);

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
				OrganizationName = educationMediumNames.GetValueOrDefault(student.AcademicMediumId),
				ClassName = standard.Name,
				DivisionName = division.Name,
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

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View)]
	public async Task<PagedResultDto<StudentAttendanceHistoryDto>> GetAttendanceHistoryAsync(GetStudentAttendanceHistoryInput input)
	{
		await _studentScopeAccessService.GetStudentAsync(input.StudentId, nameof(GetAttendanceHistoryAsync));
		var (startDate, endDate) = ResolveDateRange(input.StartDate, input.EndDate);

		var scopedAttendanceDays = await _studentScopeAccessService.ApplyCurrentUserAttendanceDayScopeAsync(
			await _attendanceDayRepository.GetQueryableAsync());
		var attendanceEntries = await _attendanceEntryRepository.GetQueryableAsync();
		var query =
			from entry in attendanceEntries
			join day in scopedAttendanceDays on entry.AttendanceDayId equals day.Id
			where entry.StudentId == input.StudentId
			      && day.Date >= startDate
			      && day.Date <= endDate
			select entry;

		var totalCount = await AsyncExecuter.CountAsync(query);
		query = ApplyHistorySorting(query, input.Sorting);

		var rows = await AsyncExecuter.ToListAsync(
			query
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount)
				.Select(e => new StudentAttendanceHistoryDto
				{
					AttendanceDayId = e.AttendanceDayId,
					Date = e.AttendanceDay.Date,
					Status = e.Status,
					Remarks = e.Remarks
				}));

		return new PagedResultDto<StudentAttendanceHistoryDto>(totalCount, rows);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Add)]
	public async Task<MarkAttendanceOutput> MarkAttendanceAsync(MarkAttendanceInput input)
	{
		// Validate inputs
		await ValidateInputsAsync(input.AcademicYearId, input.StandardId, input.DivisionId);
		await _studentScopeAccessService.EnsureStudentsMatchAttendanceScopeAsync(
			input.Entries.Select(e => e.StudentId),
			input.AcademicYearId,
			input.StandardId,
			input.DivisionId,
			nameof(MarkAttendanceAsync));

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

		await _studentScopeAccessService.EnsureCanAccessAttendanceScopeAsync(
			academicYearId,
			standardId,
			divisionId,
			nameof(ValidateInputsAsync));
	}

	#endregion

	private (DateTime StartDate, DateTime EndDate) ResolveDateRange(DateTime? startDate, DateTime? endDate)
	{
		var now = Clock.Now.Date;
		var defaultStart = new DateTime(now.Year, now.Month, 1);
		var defaultEnd = defaultStart.AddMonths(1).AddDays(-1);
		var start = (startDate ?? defaultStart).Date;
		var end = (endDate ?? defaultEnd).Date;

		if (start > end)
		{
			throw new BusinessException("StudentAttendance:InvalidDateRange")
				.WithData("StartDate", start)
				.WithData("EndDate", end);
		}

		return (start, end);
	}

	private static IOrderedQueryable<AttendanceEntry> ApplyHistorySorting(
		IQueryable<AttendanceEntry> query,
		string? sorting)
	{
		var token = (sorting ?? string.Empty)
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.FirstOrDefault();
		var parts = token?
			.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			?? [];
		var field = parts.FirstOrDefault()?.ToLowerInvariant();
		var descending = parts.Length < 2 || parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

		return field switch
		{
			"status" => descending
				? query.OrderByDescending(e => e.Status).ThenByDescending(e => e.AttendanceDay.Date).ThenBy(e => e.Id)
				: query.OrderBy(e => e.Status).ThenBy(e => e.AttendanceDay.Date).ThenBy(e => e.Id),
			"remarks" => descending
				? query.OrderByDescending(e => e.Remarks).ThenByDescending(e => e.AttendanceDay.Date).ThenBy(e => e.Id)
				: query.OrderBy(e => e.Remarks).ThenBy(e => e.AttendanceDay.Date).ThenBy(e => e.Id),
			"date" => descending
				? query.OrderByDescending(e => e.AttendanceDay.Date).ThenBy(e => e.Id)
				: query.OrderBy(e => e.AttendanceDay.Date).ThenBy(e => e.Id),
			_ => query.OrderByDescending(e => e.AttendanceDay.Date).ThenBy(e => e.Id)
		};
	}
}
