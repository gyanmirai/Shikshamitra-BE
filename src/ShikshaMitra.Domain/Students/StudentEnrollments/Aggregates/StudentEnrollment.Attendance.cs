using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.Attendance;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments;

public partial class StudentEnrollment
{
	public DateTime? AttendanceStartDate { get; private set; }
	public string AttendancePlacementHistoryJson { get; private set; } = "[]";

	public void ConfirmAttendanceStart(DateTime date)
	{
		if (AttendanceStartDate.HasValue && AttendanceStartDate.Value.Date != date.Date)
			throw new BusinessException(AttendanceOperationErrorCodes.StartDateAlreadyConfirmed);
		CaptureAttendancePlacement(date);
		AttendanceStartDate = date.Date;
	}

	public AttendanceEnrollmentPlacement? AttendancePlacementOn(DateTime date)
	{
		var history = ReadAttendancePlacements();
		return history.Where(x => x.Date <= date.Date).OrderByDescending(x => x.Date).FirstOrDefault();
	}

	public IReadOnlyList<AttendanceEnrollmentPlacement> AttendancePlacementsBetween(DateTime start, DateTime end)
	{
		var history = ReadAttendancePlacements().Where(x => x.Date <= end.Date).OrderBy(x => x.Date).ToList();
		var initial = history.LastOrDefault(x => x.Date <= start.Date);
		var result = history.Where(x => x.Date > start.Date).ToList();
		if (initial != null) result.Insert(0, initial);
		return result;
	}

	private void CaptureAttendancePlacement(DateTime? effectiveDate)
	{
		if (ReadAttendancePlacements().Count != 0) return;
		// An imported enrollment has no verified dated placement until explicitly confirmed.
		if (!AttendanceStartDate.HasValue && !effectiveDate.HasValue) return;
		var firstDate = AttendanceStartDate ?? effectiveDate!.Value.Date;
		AttendancePlacementHistoryJson = JsonSerializer.Serialize(new[] {
			new AttendanceEnrollmentPlacement(firstDate, StandardId, DivisionId, RollNumber, IsActive) });
	}

	private void RecordAttendancePlacement(DateTime? effectiveDate)
	{
		var history = ReadAttendancePlacements();
		if (history.Count == 0) return;
		var date = (effectiveDate ?? AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow)).Date;
		// Same-date placement changes replace the date's roster, never create two daily memberships.
		history.RemoveAll(x => x.Date == date);
		history.Add(new(date, StandardId, DivisionId, RollNumber, IsActive));
		AttendancePlacementHistoryJson = JsonSerializer.Serialize(history.OrderBy(x => x.Date));
	}
	private List<AttendanceEnrollmentPlacement> ReadAttendancePlacements() =>
		JsonSerializer.Deserialize<List<AttendanceEnrollmentPlacement>>(AttendancePlacementHistoryJson) ?? [];
}

public sealed record AttendanceEnrollmentPlacement(DateTime Date, Guid? StandardId, Guid? DivisionId, int? RollNumber, bool IsActive);
