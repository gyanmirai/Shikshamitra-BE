using System;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class EntranceExamSlotDetailDto : EntranceExamSlotDto
{
	public DateTime CreatedTime { get; set; }
	public DateTime? LastModifiedTime { get; set; }
}
