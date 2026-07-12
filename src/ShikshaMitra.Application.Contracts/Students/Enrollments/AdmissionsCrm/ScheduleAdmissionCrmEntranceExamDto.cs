using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class ScheduleAdmissionCrmEntranceExamDto
{
	public Guid EntranceExamSlotId { get; set; }
	public bool OverrideCapacity { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? CapacityOverrideReason { get; set; }
}
