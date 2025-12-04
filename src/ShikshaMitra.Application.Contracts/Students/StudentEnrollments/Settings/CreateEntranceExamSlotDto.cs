using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class CreateEntranceExamSlotDto
{
	[Required(ErrorMessage = "Academic Year is required")]
	public Guid AcademicYearId { get; set; }

	[MaxLength(100)]
	public string? SlotName { get; set; }

	[Required(ErrorMessage = "Date is required")]
	public DateTime Date { get; set; }

	[Required(ErrorMessage = "Start Time is required")]
	public TimeSpan StartTime { get; set; }

	[Required(ErrorMessage = "End Time is required")]
	public TimeSpan EndTime { get; set; }

	[Required(ErrorMessage = "Student Limit is required")]
	[Range(1, 1000, ErrorMessage = "Student Limit must be between 1 and 1000")]
	public int StudentLimit { get; set; }

	[Required(ErrorMessage = "Room No. is required")]
	[Range(1, 99999, ErrorMessage = "Room number must be greater than 0")]
	public int RoomNo { get; set; }
}
