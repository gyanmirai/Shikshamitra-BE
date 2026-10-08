using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>One authorized offering and published assessment stage, optionally restricted to a session roster.</summary>
public class AdmissionAssessmentRosterInput : PagedAndSortedResultRequestDto
{
	public Guid AdmissionOfferingId { get; set; }
	public Guid WorkflowStageDefinitionId { get; set; }
	public Guid? SessionId { get; set; }
	[MaxLength(128)] public string? Search { get; set; }
	[RegularExpression("^(toBook|attendance|result|review|ready|attention|completed)$")] public string? Queue { get; set; }
}

public class AdmissionAssessmentRosterDto : PagedResultDto<AdmissionAssessmentRosterItemDto>
{
	public AdmissionAssessmentQueueCountsDto Counts { get; set; } = new();
}

public class AdmissionAssessmentQueueCountsDto
{
	public int All { get; set; }
	public int ToBook { get; set; }
	public int Attendance { get; set; }
	public int Result { get; set; }
	public int Review { get; set; }
	public int Ready { get; set; }
	public int Attention { get; set; }
	public int Completed { get; set; }
}

public class AdmissionAssessmentRosterItemDto
{
	public Guid ApplicationId { get; set; }
	public string ApplicationNumber { get; set; } = string.Empty;
	public Guid ApplicantId { get; set; }
	public string ApplicantName { get; set; } = string.Empty;
	public Guid StageInstanceId { get; set; }
	public string StageName { get; set; } = string.Empty;
	public AdmissionApplicationStatus ApplicationStatus { get; set; }
	public AdmissionRuntimeStageStatus StageStatus { get; set; }
	public bool IsActive { get; set; }
	public DateTime? EnteredAt { get; set; }
	public string Queue { get; set; } = string.Empty;
	public AdmissionAssessmentDto Assessment { get; set; } = new();
}
