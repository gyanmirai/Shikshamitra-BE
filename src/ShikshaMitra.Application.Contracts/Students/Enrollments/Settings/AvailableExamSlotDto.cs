using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class AvailableExamSlotDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string? SlotName { get; set; }
	public DateTime Date { get; set; }
	public TimeSpan StartTime { get; set; }
	public TimeSpan EndTime { get; set; }
	public int RoomNo { get; set; }
	public int AvailableSeats { get; set; }
	public string DisplayText { get; set; } = string.Empty;
}
