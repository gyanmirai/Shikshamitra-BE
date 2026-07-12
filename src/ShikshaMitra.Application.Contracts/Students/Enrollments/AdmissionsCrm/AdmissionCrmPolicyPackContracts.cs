using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPolicySourceDto
{
	public string Authority { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string Url { get; set; } = string.Empty;
	public DateTime PublishedOrEffectiveAt { get; set; }
	public DateTime VerifiedAt { get; set; }
	public string ApplicabilityNote { get; set; } = string.Empty;
}

public class AdmissionCrmPolicyRuleDto
{
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public AdmissionCrmPolicyRuleType Type { get; set; }
	public AdmissionCrmPolicyRuleSeverity Severity { get; set; }
	public string Description { get; set; } = string.Empty;
	public string? DesiredClass { get; set; }
	public string? AdmissionRoute { get; set; }
	public DateTime? AgeCutoffDate { get; set; }
	public int? MinimumAgeYears { get; set; }
	public int? MaximumAgeYears { get; set; }
	public string? RequiredEvidenceCode { get; set; }
	public DateTime? Deadline { get; set; }
	public bool ManualVerificationRequired { get; set; }
	public string SourceAuthority { get; set; } = string.Empty;
	public string SourceUrl { get; set; } = string.Empty;
}

public class CreateAdmissionCrmPolicyPackDto
{
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Jurisdiction { get; set; } = string.Empty;
	public string Board { get; set; } = string.Empty;
	public IReadOnlyList<AdmissionCrmPolicySourceDto> Sources { get; set; } = [];
	public IReadOnlyList<AdmissionCrmPolicyRuleDto> Rules { get; set; } = [];
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveUntil { get; set; }
	public DateTime ReviewDueAt { get; set; }
}

public class AdmissionCrmPolicyPackDto : EntityDto<Guid>
{
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Jurisdiction { get; set; } = string.Empty;
	public string Board { get; set; } = string.Empty;
	public int Version { get; set; }
	public AdmissionCrmPolicyPackStatus Status { get; set; }
	public IReadOnlyList<AdmissionCrmPolicySourceDto> Sources { get; set; } = [];
	public IReadOnlyList<AdmissionCrmPolicyRuleDto> Rules { get; set; } = [];
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveUntil { get; set; }
	public DateTime ReviewDueAt { get; set; }
	public bool SourceReviewOverdue { get; set; }
	public Guid? SubmittedByUserId { get; set; }
	public DateTime? SubmittedAt { get; set; }
	public Guid? ApprovedByUserId { get; set; }
	public DateTime? ApprovedAt { get; set; }
	public DateTime? PublishedAt { get; set; }
	public Guid? SupersededByPolicyPackId { get; set; }
}

public class GetAdmissionCrmPolicyPackListInput : PagedAndSortedResultRequestDto
{
	public AdmissionCrmPolicyPackStatus? Status { get; set; }
	public string? Board { get; set; }
	public string? Jurisdiction { get; set; }
}

public class AdmissionCrmPolicyBaselineInstallResultDto
{
	public DateTime CatalogVerifiedAt { get; set; }
	public IReadOnlyList<AdmissionCrmPolicyPackDto> CreatedDrafts { get; set; } = [];
	public IReadOnlyList<string> AlreadyCurrentCodes { get; set; } = [];
}

public class AdmissionCrmPolicyApplicabilityDto
{
	public string? Campus { get; set; }
	public string? Board { get; set; }
	public string? Medium { get; set; }
	public string? DesiredClass { get; set; }
	public string? AdmissionRoute { get; set; }
	public string? ApplicantCategory { get; set; }
}

public class AdmissionCrmPolicyOverrideDto
{
	public string RuleCode { get; set; } = string.Empty;
	public AdmissionCrmPolicyRuleSeverity? Severity { get; set; }
	public DateTime? AgeCutoffDate { get; set; }
	public int? MinimumAgeYears { get; set; }
	public int? MaximumAgeYears { get; set; }
	public DateTime? Deadline { get; set; }
	public string Warning { get; set; } = string.Empty;
}

public class AdoptAdmissionCrmPolicyPackDto
{
	public AdmissionCrmPolicyApplicabilityDto Applicability { get; set; } = new();
	public IReadOnlyList<AdmissionCrmPolicyOverrideDto> Overrides { get; set; } = [];
	public string? OverrideReason { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveUntil { get; set; }
}

public class AdmissionCrmPolicyAdoptionDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid PolicyPackId { get; set; }
	public int PolicyPackVersion { get; set; }
	public AdmissionCrmPolicyApplicabilityDto Applicability { get; set; } = new();
	public IReadOnlyList<AdmissionCrmPolicyOverrideDto> Overrides { get; set; } = [];
	public IReadOnlyList<string> ImpactWarnings { get; set; } = [];
	public string? OverrideReason { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveUntil { get; set; }
	public Guid AdoptedByUserId { get; set; }
	public DateTime AdoptedAt { get; set; }
	public bool IsActive { get; set; }
}

public class EvaluateAdmissionCrmPolicyRuleDto
{
	public Guid PolicyAdoptionId { get; set; }
	public string RuleCode { get; set; } = string.Empty;
	public AdmissionCrmPolicyEvaluationStatus? ManualStatus { get; set; }
	public IReadOnlyList<string> EvidenceReferences { get; set; } = [];
	public string? Explanation { get; set; }
}

public class AdmissionCrmPolicyEvaluationDto : EntityDto<Guid>
{
	public Guid LeadId { get; set; }
	public Guid PolicyAdoptionId { get; set; }
	public string RuleCode { get; set; } = string.Empty;
	public AdmissionCrmPolicyRuleType RuleType { get; set; }
	public AdmissionCrmPolicyRuleSeverity Severity { get; set; }
	public AdmissionCrmPolicyEvaluationStatus Status { get; set; }
	public IReadOnlyDictionary<string, object?> InputSnapshot { get; set; } = new Dictionary<string, object?>();
	public IReadOnlyList<string> EvidenceReferences { get; set; } = [];
	public string Explanation { get; set; } = string.Empty;
	public DateTime EvaluatedAt { get; set; }
	public Guid? ExceptionId { get; set; }
}

public class RequestAdmissionCrmPolicyExceptionDto
{
	public string Reason { get; set; } = string.Empty;
	public string EvidenceReference { get; set; } = string.Empty;
	public DateTime? ExpiresAt { get; set; }
}

public class DecideAdmissionCrmPolicyExceptionDto
{
	public bool Approve { get; set; }
	public string Reason { get; set; } = string.Empty;
}

public class AdmissionCrmPolicyExceptionDto : EntityDto<Guid>
{
	public Guid LeadId { get; set; }
	public Guid EvaluationId { get; set; }
	public AdmissionCrmPolicyExceptionStatus Status { get; set; }
	public string Reason { get; set; } = string.Empty;
	public string EvidenceReference { get; set; } = string.Empty;
	public Guid RequestedByUserId { get; set; }
	public DateTime RequestedAt { get; set; }
	public Guid? DecidedByUserId { get; set; }
	public DateTime? DecidedAt { get; set; }
	public string? DecisionReason { get; set; }
	public DateTime? ExpiresAt { get; set; }
}

public class AdmissionCrmPolicyReadinessDto
{
	public Guid LeadId { get; set; }
	public IReadOnlyList<AdmissionCrmPolicyEvaluationDto> Evaluations { get; set; } = [];
	public IReadOnlyList<AdmissionCrmPolicyExceptionDto> Exceptions { get; set; } = [];
	public int BlockingFailureCount { get; set; }
	public int PendingEvidenceCount { get; set; }
	public bool IsPolicyReady { get; set; }
}
