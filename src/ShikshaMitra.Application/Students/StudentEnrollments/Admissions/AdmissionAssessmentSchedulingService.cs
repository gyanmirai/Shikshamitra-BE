using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public partial class AdmissionAssessmentSchedulingService(
	IRepository<Room, Guid> rooms,
	IRepository<RoomReservation, Guid> reservations,
	IRepository<Employee, Guid> employees,
	IRepository<AdmissionAssessmentSession, Guid> sessions,
	IRepository<AdmissionAssessmentAttempt, Guid> attempts,
	IRepository<AdmissionApplication, Guid> applications,
	RoomConflictService conflicts,
	AdmissionInquiryPeopleService people) : ApplicationService, ITransientDependency
{
	public static (DateTime Date, TimeSpan Start, TimeSpan End) SchoolWindow(DateTime starts, DateTime ends)
	{
		TimeZoneInfo zone;
		try { zone = TimeZoneInfo.FindSystemTimeZoneById(AdmissionCycleWindowEvaluator.InstitutionTimeZoneId); }
		catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
		var start = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(starts, DateTimeKind.Utc), zone);
		var end = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(ends, DateTimeKind.Utc), zone);
		if (start.Date != end.Date || end <= start) throw Invalid("An assessment session must start and end on the same school day.");
		return (DateTime.SpecifyKind(start.Date, DateTimeKind.Utc), start.TimeOfDay, end.TimeOfDay);
	}

	public async Task<Room> ValidateAsync(AdmissionAssessmentSessionInput input, IReadOnlyCollection<AdmissionOffering> offerings, Guid? ignoreSessionId = null, Guid? ignoreReservationId = null)
	{
		if (!input.RoomId.HasValue || input.SessionType is not ("exam" or "individualInterview" or "groupInterview")) throw Invalid("Choose a school room and assessment type.");
		var room = await rooms.GetAsync(input.RoomId.Value);
		if (UsableCapacity(room) < 1 || input.Capacity < 1 || input.Capacity > 10000 || input.SessionType != "individualInterview" && input.Capacity > UsableCapacity(room)) throw Invalid("Session capacity must fit the room's usable seats.");
		if (input.SessionType != "exam")
		{
			var employee = input.InterviewerId.HasValue ? await employees.FindAsync(input.InterviewerId.Value) : null;
			var schoolDate = DateOnly.FromDateTime(SchoolWindow(input.StartsAt.UtcDateTime, input.EndsAt.UtcDateTime).Date);
			if (employee == null || employee.DateOfJoining.HasValue && employee.DateOfJoining.Value > schoolDate || !EmployeeLifecycleEligibility.IsAttendanceEligibleOnDate(employee.EmployeeStatusType, employee.StatusEffectiveDate, employee.StatusLastChangedAt, schoolDate) || !employee.UserId.HasValue)
				throw Invalid("Choose an active interviewer with a staff account.");
			foreach (var offering in offerings)
				if (!await people.CanAccessOfferingAsync(employee.UserId.Value, offering.Id)) throw Invalid("The interviewer needs access to every selected offering.");
			await EnsureInterviewerAvailableAsync(employee, input);
		}
		else if (input.InterviewerId.HasValue) throw Invalid("An exam does not require an interview slot assessor.");
		var window = SchoolWindow(input.StartsAt.UtcDateTime, input.EndsAt.UtcDateTime);
		var calendarScopes = await LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentCalendarScopeService>().GetAsync(offerings);
		foreach (var calendarScope in calendarScopes)
		{
			var result = await conflicts.CheckScopedConflictsAsync(calendarScope, room.Id, window.Date, window.Start, window.End,
				ignoreReservationId, input.InterviewerId);
			if (result.OverallSeverity == "critical") throw Invalid(string.Join(" ", result.Conflicts.Where(item => item.Severity == "critical").Select(item => item.Message)));
			if (input.InterviewerId.HasValue && !result.TeacherAvailability.Passed) throw Invalid(result.TeacherAvailability.Message ?? "The interviewer is occupied at this time.");
		}
		if (input.InterviewerId.HasValue)
			await EnsureInterviewerReservationAvailableAsync(input.InterviewerId.Value, window.Date, window.Start, window.End, ignoreReservationId);
		if (await sessions.AnyAsync(item => item.Id != ignoreSessionId && !item.IsCancelled && item.StartsAt < input.EndsAt.UtcDateTime && item.EndsAt > input.StartsAt.UtcDateTime &&
			(item.RoomId == input.RoomId || input.InterviewerId.HasValue && item.InterviewerId == input.InterviewerId)))
			throw Invalid("The room or interviewer already has an assessment at this time.");
		return room;
	}

	internal async Task EnsureInterviewerReservationAvailableAsync(Guid interviewerId, DateTime schoolDate, TimeSpan startsAt, TimeSpan endsAt, Guid? ignoreReservationId = null)
	{
		// Match Room Conflict's bounded day-window pattern. SQLite cannot translate TimeSpan comparisons;
		// only this interviewer's live reservations on this exact day are read, with no entity graphs.
		var query = (await reservations.GetQueryableAsync()).Where(item => item.Id != ignoreReservationId && item.TeacherId == interviewerId &&
			item.Date == schoolDate && (item.Status == RoomReservationStatus.PendingApproval || item.Status == RoomReservationStatus.Approved ||
			item.Status == RoomReservationStatus.Active || item.Status == RoomReservationStatus.CheckedIn));
		var windows = await AsyncExecuter.ToListAsync(query.Select(item => new { item.StartTime, item.EndTime }));
		if (windows.Any(item => item.StartTime < endsAt && item.EndTime > startsAt))
			throw Invalid("The interviewer already has a room booking at this time.");
	}

	public async Task EnsureApplicantAvailableAsync(AdmissionAssessmentContext context, AdmissionAssessmentSession session, DateTime? startsAt)
	{
		var start = startsAt ?? session.StartsAt;
		var end = startsAt.HasValue ? start.AddMinutes(session.SlotDurationMinutes!.Value) : session.EndsAt;
		var query = from attempt in await attempts.GetQueryableAsync()
			join application in await applications.GetQueryableAsync() on attempt.ApplicationId equals application.Id
			join bookedSession in await sessions.GetQueryableAsync() on attempt.SessionId equals bookedSession.Id
			where application.ApplicantId == context.Application.ApplicantId && !bookedSession.IsCancelled && attempt.Status != "cancelled" && attempt.Outcome != "waived" &&
				(attempt.SlotStartsAt ?? bookedSession.StartsAt) < end && (attempt.SlotEndsAt ?? bookedSession.EndsAt) > start
			select attempt.Id;
		if (await AsyncExecuter.AnyAsync(query)) throw Invalid("This applicant already has an assessment appointment at this time.");
	}
	public async Task<Dictionary<Guid, List<(DateTime Start, DateTime End)>>> GetApplicantAppointmentsAsync(Guid[] applicantIds, DateTime startsAt, DateTime endsAt, Guid? ignoreSessionId = null)
	{
		var query = from attempt in await attempts.GetQueryableAsync()
			join application in await applications.GetQueryableAsync() on attempt.ApplicationId equals application.Id
			join session in await sessions.GetQueryableAsync() on attempt.SessionId equals session.Id
			where Enumerable.Contains(applicantIds, application.ApplicantId) && session.Id != ignoreSessionId && !session.IsCancelled && attempt.Status != "cancelled" && attempt.Outcome != "waived" &&
				(attempt.SlotStartsAt ?? session.StartsAt) < endsAt && (attempt.SlotEndsAt ?? session.EndsAt) > startsAt
			select new { application.ApplicantId, Start = attempt.SlotStartsAt ?? session.StartsAt, End = attempt.SlotEndsAt ?? session.EndsAt };
		return (await AsyncExecuter.ToListAsync(query)).GroupBy(item => item.ApplicantId).ToDictionary(group => group.Key, group => group.Select(item => (item.Start, item.End)).ToList());
	}

	public async Task<bool> IsReservationApprovedAsync(AdmissionAssessmentSession session)
	{
		if (!session.RoomReservationId.HasValue) return true;
		var reservation = await reservations.FindAsync(session.RoomReservationId.Value);
		return reservation?.Status is RoomReservationStatus.Approved or RoomReservationStatus.Active or RoomReservationStatus.CheckedIn or RoomReservationStatus.Completed;
	}
	public async Task EnsureSessionReservationAsync(AdmissionAssessmentSession session)
	{
		if (!session.RoomReservationId.HasValue) return;
		var reservation = await reservations.FindAsync(session.RoomReservationId.Value);
		if (reservation?.Status is not (RoomReservationStatus.PendingApproval or RoomReservationStatus.Approved or RoomReservationStatus.Active or RoomReservationStatus.CheckedIn))
			throw Invalid("The session's room reservation is no longer available. Reschedule the session before booking applicants.");
	}
	public async Task EnrichReservationsAsync(IEnumerable<AdmissionAssessmentSession> sessionRows)
	{
		var rows = sessionRows.ToList();
		var ids = rows.Where(item => item.RoomReservationId.HasValue).Select(item => item.RoomReservationId!.Value).Distinct().ToArray();
		var statuses = (await reservations.GetListAsync(item => Enumerable.Contains(ids, item.Id))).ToDictionary(item => item.Id, item => (int)item.Status);
		foreach (var row in rows) row.RoomReservationStatus = row.RoomReservationId.HasValue ? statuses.GetValueOrDefault(row.RoomReservationId.Value) : null;
	}

	public static int UsableCapacity(Room room)
	{
		var unavailable = ((room.DisabledSeatNumbersCsv ?? "") + "," + (room.ReservedSeatNumbersCsv ?? "")).Split(',', StringSplitOptions.RemoveEmptyEntries)
			.Select(value => int.TryParse(value.Trim(), out var seat) ? seat : 0).Where(seat => seat > 0 && seat <= room.Capacity).Distinct().Count();
		return Math.Max(0, room.Capacity - unavailable);
	}
	internal static BusinessException Invalid(string message) =>
		new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, message).WithData("Reason", message);
}
