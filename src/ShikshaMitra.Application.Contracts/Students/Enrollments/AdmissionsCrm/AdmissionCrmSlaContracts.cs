using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmSlaPolicyDto
{
	public string TimeZoneId { get; set; } = "Asia/Kolkata";
	public IReadOnlyList<DayOfWeek> WorkingDays { get; set; } = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday];
	public int WorkingDayStartHour { get; set; } = 8;
	public int WorkingDayEndHour { get; set; } = 17;
	public IReadOnlyList<DateTime> Holidays { get; set; } = [];
	public int EscalationAfterHours { get; set; } = 24;
	public Guid? EscalationOwnerUserId { get; set; }
}

public class CreateAdmissionCrmSlaHoldDto
{
	public AdmissionCrmSlaHoldReason HoldReason { get; set; }
	public string Reason { get; set; } = string.Empty;
}

public class ResumeAdmissionCrmSlaHoldDto { public string Reason { get; set; } = string.Empty; }

public class AdmissionCrmSlaHoldDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid LeadId { get; set; }
	public AdmissionCrmSlaHoldReason HoldReason { get; set; }
	public string Reason { get; set; } = string.Empty;
	public DateTime StartedAt { get; set; }
	public DateTime? ResumedAt { get; set; }
	public string? ResumeReason { get; set; }
	public bool IsActive { get; set; }
}
