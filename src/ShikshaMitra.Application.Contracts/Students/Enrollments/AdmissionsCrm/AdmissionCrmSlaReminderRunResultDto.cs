using System;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmSlaReminderRunResultDto
{
	public int CreatedTaskCount { get; set; }
	public int SkippedExistingTaskCount { get; set; }
	public int SlaThresholdHours { get; set; }
	public int StageOverrideCount { get; set; }
	public int PausedLeadCount { get; set; }
	public int EscalatedTaskCount { get; set; }
	public DateTimeOffset SlaCutoff { get; set; }
}
