using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Filters the authorized, ongoing inquiry-backed admission work queue.</summary>
public class AdmissionPipelineListInput : AdmissionApplicationListInput
{
	public bool? MyCases { get; set; }
	[MaxLength(64)] public string? CurrentStageCode { get; set; }
}

public class AdmissionPipelineItemDto
{
	public Guid Id { get; set; }
	public string ApplicationNumber { get; set; } = string.Empty;
	public Guid ApplicantId { get; set; }
	public string ApplicantName { get; set; } = string.Empty;
	public Guid InquiryId { get; set; }
	public bool SourceInquiryAvailable { get; set; }
	public string? InquiryNumber { get; set; }
	public string? ContactPhone { get; set; }
	public string? ContactEmail { get; set; }
	public string OfferingCode { get; set; } = string.Empty;
	public string StandardName { get; set; } = string.Empty;
	public AdmissionApplicationStatus Status { get; set; }
	public Guid? CurrentStageInstanceId { get; set; }
	public string? CurrentStageCode { get; set; }
	public string? CurrentStageName { get; set; }
	public AdmissionWorkflowStageType? StageType { get; set; }
	public string NextRequiredAction { get; set; } = string.Empty;
	public DateTime CreationTime { get; set; }
	public DateTime? EnteredAt { get; set; }
	public DateTime? DueAt { get; set; }
	public Guid? AssignedCounsellorId { get; set; }
	public string? AssignedCounsellorName { get; set; }
	public int OpenFollowUpCount { get; set; }
	public DateTime? NextFollowUpAt { get; set; }
	public bool HasOverdueFollowUp { get; set; }
}

public class AdmissionPipelineStageOptionDto
{
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
}

public class AdmissionPipelineOptionsDto
{
	public Guid? CurrentUserId { get; set; }
	public bool DefaultMyCases { get; set; }
	public List<AdmissionInquiryCounsellorDto> Counsellors { get; set; } = [];
	public List<AdmissionPipelineStageOptionDto> Stages { get; set; } = [];
}
