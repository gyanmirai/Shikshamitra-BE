using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Attendance.Settings;

public class StudentAttendanceRuleDto : AuditedEntityDto<Guid>
{
	[Required] public string Name { get; set; }
	public StudentAttendanceStatusType Type { get; set; }
	public int LateThreshold { get; set; }
	public TimeUnitType LateThresholdUnit { get; set; }
	public int HalfDayThreshold { get; set; }
	public TimeUnitType HalfDayThresholdUnit { get; set; }
	public bool EnableNotificationForUpperAuthority { get; set; }
}