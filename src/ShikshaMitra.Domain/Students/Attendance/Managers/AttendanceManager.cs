using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.Users;

namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// Domain service for managing student attendance operations.
/// Encapsulates business rules and validation logic.
/// </summary>
public class AttendanceManager : DomainService
{
	private readonly IAttendanceDayRepository _attendanceDayRepository;
	private readonly IAttendanceEntryRepository _attendanceEntryRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IGuidGenerator _guidGenerator;
	private readonly ICurrentUser _currentUser;

	public AttendanceManager(
		IAttendanceDayRepository attendanceDayRepository,
		IAttendanceEntryRepository attendanceEntryRepository,
		IRepository<Student, Guid> studentRepository,
		IGuidGenerator guidGenerator,
		ICurrentUser currentUser)
	{
		_attendanceDayRepository = attendanceDayRepository;
		_attendanceEntryRepository = attendanceEntryRepository;
		_studentRepository = studentRepository;
		_guidGenerator = guidGenerator;
		_currentUser = currentUser;
	}

	/// <summary>
	/// Gets or creates an AttendanceDay for the specified date and division
	/// </summary>
	public async Task<AttendanceDay> GetOrCreateAttendanceDayAsync(
		Guid academicYearId,
		Guid standardId,
		Guid divisionId,
		DateTime date)
	{
		var attendanceDay = await _attendanceDayRepository.FindByDateAndDivisionAsync(
			academicYearId,
			standardId,
			divisionId,
			date.Date);

		if (attendanceDay == null)
		{
			var userId = _currentUser.Id ?? throw new BusinessException("SM:UserNotAuthenticated");

			attendanceDay = new AttendanceDay(
				_guidGenerator.Create(),
				CurrentTenant.Id,
				academicYearId,
				standardId,
				divisionId,
				date.Date,
				userId);

			attendanceDay = await _attendanceDayRepository.InsertAsync(attendanceDay, autoSave: true);
		}

		return attendanceDay;
	}

	/// <summary>
	/// Marks or updates attendance for multiple students
	/// </summary>
	public async Task MarkAttendanceAsync(
		Guid attendanceDayId,
		List<AttendanceEntryInput> entries)
	{
		var attendanceDay = await _attendanceDayRepository.GetAsync(attendanceDayId);

		// Ensure the day is not locked
		attendanceDay.EnsureNotLocked();

		var userId = _currentUser.Id ?? throw new BusinessException("SM:UserNotAuthenticated");

		// Update the marked by user
		attendanceDay.UpdateMarkedByUser(userId);

		foreach (var entry in entries)
		{
			await UpsertAttendanceEntryAsync(
				attendanceDayId,
				entry.StudentId,
				entry.Status,
				userId,
				entry.Remarks);
		}

		await _attendanceDayRepository.UpdateAsync(attendanceDay, autoSave: true);
	}

	/// <summary>
	/// Upserts a single attendance entry
	/// </summary>
	private async Task UpsertAttendanceEntryAsync(
		Guid attendanceDayId,
		Guid studentId,
		AttendanceStatus status,
		Guid markedByUserId,
		string? remarks)
	{
		var existingEntry = await _attendanceEntryRepository.FindByAttendanceDayAndStudentAsync(
			attendanceDayId,
			studentId);

		if (existingEntry != null)
		{
			// Update existing entry
			existingEntry.UpdateStatus(status, markedByUserId);
			existingEntry.SetRemarks(remarks);
			await _attendanceEntryRepository.UpdateAsync(existingEntry);
		}
		else
		{
			// Create new entry
			var newEntry = new AttendanceEntry(
				_guidGenerator.Create(),
				CurrentTenant.Id,
				attendanceDayId,
				studentId,
				status,
				markedByUserId,
				remarks);

			await _attendanceEntryRepository.InsertAsync(newEntry);
		}
	}

	/// <summary>
	/// Locks an attendance day to prevent further modifications
	/// </summary>
	public async Task LockAttendanceDayAsync(Guid attendanceDayId)
	{
		var attendanceDay = await _attendanceDayRepository.GetAsync(attendanceDayId);
		attendanceDay.Lock();
		await _attendanceDayRepository.UpdateAsync(attendanceDay, autoSave: true);
	}

	/// <summary>
	/// Unlocks an attendance day to allow modifications
	/// </summary>
	public async Task UnlockAttendanceDayAsync(Guid attendanceDayId)
	{
		var attendanceDay = await _attendanceDayRepository.GetAsync(attendanceDayId);
		attendanceDay.Unlock();
		await _attendanceDayRepository.UpdateAsync(attendanceDay, autoSave: true);
	}

	/// <summary>
	/// Validates that all students exist
	/// </summary>
	public async Task ValidateStudentsAsync(List<Guid> studentIds)
	{
		foreach (var studentId in studentIds)
		{
			if (!await _studentRepository.AnyAsync(s => s.Id == studentId))
			{
				throw new BusinessException("SM:StudentNotFound")
					.WithData("StudentId", studentId);
			}
		}
	}

	/// <summary>
	/// Input model for marking attendance entry
	/// </summary>
	public class AttendanceEntryInput
	{
		public Guid StudentId { get; set; }
		public AttendanceStatus Status { get; set; }
		public string? Remarks { get; set; }
	}
}
