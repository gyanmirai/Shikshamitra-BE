using System;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class UpdateEntranceExamSlotDto
{
	public string? SlotName { get; set; }
	public DateTimeOffset StartDateTime { get; set; }
	public DateTimeOffset EndDateTime { get; set; }
	public int StudentLimit { get; set; }
	public int RoomNo { get; set; }
}
