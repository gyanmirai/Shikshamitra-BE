using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class EntranceExamSlotDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string? SlotName { get; set; }
	public DateTimeOffset StartDateTime { get; set; }
	public DateTimeOffset EndDateTime { get; set; }
	public int Duration { get; set; } // In minutes
	public int StudentLimit { get; set; }
	public int RoomNo { get; set; }
	public int? EnrolledStudents { get; set; }
	public int? AvailableSeats { get; set; }
}
