using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionEligibilityDto
{
	public Guid ApplicationId { get; set; }
	public Guid StageId { get; set; }
	public bool EligibilityEnabled { get; set; }
	public AdmissionEligibilityPolicy? Policy { get; set; }
	public bool IsActive { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public string Status { get; set; } = "notChecked";
	public bool IsStale { get; set; }
	public bool CanEvaluate { get; set; }
	public bool CanRequestException { get; set; }
	public bool CanReviewException { get; set; }
	public bool CanComplete { get; set; }
	public List<string> Blockers { get; set; } = [];
	public AdmissionEligibilityEvaluationDto? LatestEvaluation { get; set; }
}
public class AdmissionEligibilityEvaluationDto
{
	public Guid Id { get; set; }
	public int SequenceNumber { get; set; }
	public string Outcome { get; set; } = string.Empty;
	public Guid EvaluatedBy { get; set; }
	public DateTime EvaluatedAt { get; set; }
	public bool IsStale { get; set; }
	public List<AdmissionEligibilityCriterionResultDto> Criteria { get; set; } = [];
	public List<AdmissionEligibilityExceptionDto> Exceptions { get; set; } = [];
}
public class AdmissionEligibilityCriterionResultDto
{
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Kind { get; set; } = string.Empty;
	public string Outcome { get; set; } = string.Empty;
	public string Expected { get; set; } = string.Empty;
	public string Actual { get; set; } = string.Empty;
	public string Source { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
}
public class AdmissionEligibilityExceptionDto
{
	public Guid Id { get; set; }
	public Guid EvaluationId { get; set; }
	public string Reason { get; set; } = string.Empty;
	public Guid RequestedBy { get; set; }
	public DateTime RequestedAt { get; set; }
	public string Status { get; set; } = "pending";
	public string? ReviewReason { get; set; }
	public Guid? ReviewedBy { get; set; }
	public DateTime? ReviewedAt { get; set; }
	public bool CanReview { get; set; }
}
public class AdmissionEligibilityEvaluateDto
{
	[Required, MaxLength(40)] public string ConcurrencyStamp { get; set; } = string.Empty;
}
public class AdmissionEligibilityExceptionRequestDto : AdmissionEligibilityEvaluateDto
{
	[Required, MaxLength(1024)] public string Reason { get; set; } = string.Empty;
}
public class AdmissionEligibilityExceptionReviewDto : AdmissionEligibilityExceptionRequestDto
{
	[Required] public string Decision { get; set; } = string.Empty;
}
public class AdmissionEligibilityRoleDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
}
