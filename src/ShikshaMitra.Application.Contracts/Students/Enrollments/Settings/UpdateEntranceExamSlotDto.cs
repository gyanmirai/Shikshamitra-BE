using System;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class UpdateEntranceExamSlotDto
{
	public string? SlotName { get; set; }
	public DateTime Date { get; set; }
	public TimeSpan StartTime { get; set; }
	public TimeSpan EndTime { get; set; }
	public int StudentLimit { get; set; }
	public int RoomNo { get; set; }
}
