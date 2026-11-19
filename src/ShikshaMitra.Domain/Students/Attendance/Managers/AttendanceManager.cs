using System;
using System.Collections.Generic;
using System.Linq;
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
		List<AttendanceEntryInput> entries, bool approvedCorrection = false)
	{
		var attendanceDay = await _attendanceDayRepository.GetAsync(attendanceDayId);

		// Ensure the day is not locked
		if (!approvedCorrection) attendanceDay.EnsureNotLocked();

		var userId = _currentUser.Id ?? throw new BusinessException("SM:UserNotAuthenticated");

		// Update the marked by user
		attendanceDay.UpdateMarkedByUser(userId);

		// Read the day's entries once instead of querying once per student.
		var existing = (await _attendanceEntryRepository.GetListAsync(e => e.AttendanceDayId == attendanceDayId))
			.ToDictionary(e => e.StudentId);
		var inserts = new List<AttendanceEntry>();
		var updates = new List<AttendanceEntry>();
		foreach (var input in entries)
		{
			if (existing.TryGetValue(input.StudentId, out var entry))
			{
				entry.UpdateStatus(input.Status, userId);
				entry.SetRemarks(input.Remarks);
				ApplyDetails(entry, input);
				updates.Add(entry);
			}
			else
			{
				entry = new AttendanceEntry(_guidGenerator.Create(), CurrentTenant.Id, attendanceDayId,
					input.StudentId, input.Status, userId, input.Remarks);
				ApplyDetails(entry, input);
				inserts.Add(entry);
			}
		}
		if (inserts.Count > 0) await _attendanceEntryRepository.InsertManyAsync(inserts);
		if (updates.Count > 0) await _attendanceEntryRepository.UpdateManyAsync(updates);

		await _attendanceDayRepository.UpdateAsync(attendanceDay, autoSave: true);
	}

	private static void ApplyDetails(AttendanceEntry entry, AttendanceEntryInput input)
	{
		if (input.EnrollmentId.HasValue)
			entry.SetDailyDetails(input.EnrollmentId.Value, input.Date, input.Late, input.HalfDay,
				input.PresentCredit, input.ExcludedFromPercentage, input.ApprovedLeaveId, input.PolicyJson, input.HistoricalRollNo, input.DenominatorCredit);
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
		public Guid? EnrollmentId { get; set; }
		public DateTime Date { get; set; }
		public bool Late { get; set; }
		public bool HalfDay { get; set; }
		public double? PresentCredit { get; set; }
		public bool ExcludedFromPercentage { get; set; }
		public Guid? ApprovedLeaveId { get; set; }
		public string PolicyJson { get; set; } = "{}";
		public int? HistoricalRollNo { get; set; }
		public double DenominatorCredit { get; set; } = 1;
	}
}
