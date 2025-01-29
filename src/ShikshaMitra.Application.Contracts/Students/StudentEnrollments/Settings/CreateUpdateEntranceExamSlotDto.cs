using System;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class CreateUpdateEntranceExamSlotDto
{
	public DateTimeOffset StartDate { get; set; }
	public DateTimeOffset EndDate { get; set; }
}