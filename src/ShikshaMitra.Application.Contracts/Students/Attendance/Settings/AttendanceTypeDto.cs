using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Attendance.Settings;

public class AttendanceTypeDto : AuditedEntityDto<Guid>
{
	public string Type { get;  set; }
	public string Color { get;  set; }
}