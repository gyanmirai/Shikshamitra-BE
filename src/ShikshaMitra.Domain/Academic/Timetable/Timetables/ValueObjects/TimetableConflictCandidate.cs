using System;

namespace ShikshaMitra.Academic.Timetable.Timetables;

/// <summary>
/// A competing assignment projected through its tenant-filtered timetable root.
/// </summary>
public sealed record TimetableConflictCandidate(
	Guid StandardId,
	Guid DivisionId,
	Guid SubjectId,
	TimetableDayOfWeek Day,
	TimeOnly StartTime,
	TimeOnly EndTime,
	Guid? TeacherId,
	Guid? RoomId);
