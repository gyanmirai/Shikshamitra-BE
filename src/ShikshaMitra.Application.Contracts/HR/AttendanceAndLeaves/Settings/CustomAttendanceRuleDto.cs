using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class CustomAttendanceRuleDto : EntityDto<Guid>
{
	[Required] public string Name { get; set; }
	public EmployeeAttendanceStatusType Type { get; set; }
	public int LateThreshold { get; set; }
	public TimeUnitType LateThresholdUnit { get; set; }
	public int HalfDayThreshold { get; set; }
	public TimeUnitType HalfDayThresholdUnit { get; set; }
	public bool EnableNotificationForUpperAuthority { get; set; }
}