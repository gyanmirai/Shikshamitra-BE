using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.Enrollments.EntranceExams;

/// <summary>
/// DTO for scheduling a student to an entrance exam slot
/// </summary>
public class ScheduleEntranceExamDto
{
	[Required]
	public Guid EntranceExamSlotId { get; set; }
}
