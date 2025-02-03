using System;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class CreateUpdateEntranceExamSlotDto
{
	public int StudentLimit { get; set; }
	public int RoomNo { get; set; }
	public DateTimeOffset StartDate { get; set; }
	public DateTimeOffset EndDate { get; set; }
}