using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class ResumeAdmissionJourneyDto
{
	[Required, StringLength(512)] public string Reason { get; set; } = string.Empty;
	[Required] public string ConcurrencyStamp { get; set; } = string.Empty;
	public Guid ClosureHistoryId { get; set; }
	[Required, StringLength(80)] public string IdempotencyKey { get; set; } = string.Empty;
}

public class AdmissionJourneyResumePreviewDto
{
	public bool CanResume { get; set; }
	public Guid? ClosureHistoryId { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public Guid? TargetStageId { get; set; }
	public string TargetStageName { get; set; } = string.Empty;
	public string TargetWorkspace { get; set; } = "Pipeline";
	public List<string> Blockers { get; set; } = [];
	public List<string> Warnings { get; set; } = [];
}
