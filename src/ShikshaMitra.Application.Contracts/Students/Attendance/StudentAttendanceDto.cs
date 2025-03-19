using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Attendance;

public class StudentAttendanceDto : AuditedEntityDto<Guid>
{
	public Guid StudentId { get; set; }
	public DateTimeOffset AttendanceDate { get; set; }
	public bool IsPresent { get; set; }
	public string? Remarks { get; set; }
}
