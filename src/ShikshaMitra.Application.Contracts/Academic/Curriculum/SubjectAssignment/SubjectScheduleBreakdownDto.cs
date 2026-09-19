using System;
using System.Collections.Generic;
using ShikshaMitra.Academic.Timetable;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectScheduleBreakdownDto
{
	public Guid SubjectAssignmentId { get; set; }
	public Guid StandardId { get; set; }
	public int AllocatedPeriodsPerWeek { get; set; }
	public int ScheduledPeriods { get; set; }
	public int DivisionCount { get; set; }
	public List<SubjectScheduleLectureDto> Lectures { get; set; } = [];
}

public class SubjectScheduleLectureDto
{
	public Guid TimetableId { get; set; }
	public Guid TimetableSlotId { get; set; }
	public TimetableStatus TimetableStatus { get; set; }
	public Guid DivisionId { get; set; }
	public string DivisionName { get; set; } = string.Empty;
	public TimetableDayOfWeek Day { get; set; }
	public int PeriodNumber { get; set; }
	public string StartTime { get; set; } = string.Empty;
	public string EndTime { get; set; } = string.Empty;
	public List<SubjectScheduleLectureSubjectDto> Subjects { get; set; } = [];
}

public class SubjectScheduleLectureSubjectDto
{
	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; } = string.Empty;
	public string? SubjectColor { get; set; }
	public Guid? TeacherId { get; set; }
	public string? TeacherName { get; set; }
	public Guid? RoomId { get; set; }
	public string? RoomName { get; set; }
}
