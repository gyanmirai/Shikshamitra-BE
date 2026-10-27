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
	public Guid? AcademicYearId { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public Guid? StandardId { get; set; }
	[MaxLength(800)] public string? StandardIds { get; set; }
	public Guid? SessionId { get; set; }
	[MaxLength(128)] public string? Search { get; set; }
	[MaxLength(12000)] public string? ColumnFilters { get; set; }
	[RegularExpression("^(pending|ongoing|complete)$")] public string? Status { get; set; }
	[RegularExpression("^(toBook|attendance|result|review|ready|attention|completed)$")] public string? Queue { get; set; }
}

public class AdmissionAssessmentRosterDto : PagedResultDto<AdmissionAssessmentRosterItemDto>
{
	public AdmissionAssessmentQueueCountsDto Counts { get; set; } = new();
	public AdmissionAssessmentStatusCountsDto StatusCounts { get; set; } = new();
}

public class AdmissionAssessmentStatusCountsDto
{
	public int All { get; set; }
	public int Pending { get; set; }
	public int Ongoing { get; set; }
	public int Complete { get; set; }
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
	public string ClassName { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public Guid AdmissionOfferingId { get; set; }
	public Guid WorkflowStageDefinitionId { get; set; }
	public string OfferingName { get; set; } = string.Empty;
	public int WorkflowVersion { get; set; }
	public string? InterviewerName { get; set; }
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
