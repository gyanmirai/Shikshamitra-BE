using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class EntranceExamSlotDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string? SlotName { get; set; }
	public DateTime Date { get; set; }
	public TimeSpan StartTime { get; set; }
	public TimeSpan EndTime { get; set; }
	public int Duration { get; set; } // In minutes
	public int StudentLimit { get; set; }
	public int RoomNo { get; set; }
	public int EnrolledStudents { get; set; } // Count of students scheduled for this slot
	public int AvailableSeats { get; set; } // StudentLimit - EnrolledStudents
}
