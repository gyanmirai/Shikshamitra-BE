using System;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class CreateEntranceExamSlotDto
{
	public Guid AcademicYearId { get; set; }
	public string? SlotName { get; set; }
	public DateTimeOffset StartTimeDate { get; set; }
	public DateTimeOffset EndTimeDate { get; set; }
	public int StudentLimit { get; set; }
	public int RoomNo { get; set; }
}
