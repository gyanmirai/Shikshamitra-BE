using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRouteConfigurationDto
{
	public bool RequiresPolicyEvaluation { get; set; } = true;
	public bool RequiresDistinctApproval { get; set; } = true;
	public bool AllowsExamWaiver { get; set; }
	public bool AllowsOfferWaiver { get; set; }
	public string? SeatPoolCode { get; set; }
	public string? FeePolicyCode { get; set; }
	public IReadOnlyList<string> RequiredEvidenceCodes { get; set; } = [];
	public int? PriorityWeight { get; set; }
	public int? DecisionSlaHours { get; set; }
}

public class CreateAdmissionCrmRouteDefinitionDto
{
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public AdmissionCrmRouteType RouteType { get; set; }
	public AdmissionCrmRouteConfigurationDto Configuration { get; set; } = new();
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveUntil { get; set; }
}

public class AdmissionCrmRouteDefinitionDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public AdmissionCrmRouteType RouteType { get; set; }
	public int Version { get; set; }
	public AdmissionCrmRouteDefinitionStatus Status { get; set; }
	public AdmissionCrmRouteConfigurationDto Configuration { get; set; } = new();
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveUntil { get; set; }
}

public class AdmissionCrmSpecialRouteDataDto
{
	public Guid? RelatedStudentId { get; set; }
	public Guid? FormerStudentId { get; set; }
	public string? AuthoritativeReference { get; set; }
	public string? AuthorityName { get; set; }
	public string? AllotmentNumber { get; set; }
	public string? AllotmentRound { get; set; }
	public string? AllottedCampus { get; set; }
	public string? AllottedClass { get; set; }
	public DateTime? ParentAcceptanceDeadline { get; set; }
	public DateTime? AdmissionDeadline { get; set; }
	public string? PreviousSchool { get; set; }
	public string? PreviousBoard { get; set; }
	public string? LastClassCompleted { get; set; }
	public string? TransferCertificateStatus { get; set; }
	public string? EquivalenceStatus { get; set; }
	public string? Country { get; set; }
	public DateTime? ProposedJoiningDate { get; set; }
	public string? Notes { get; set; }
}

public class AssignAdmissionCrmSpecialRouteDto
{
	public Guid RouteDefinitionId { get; set; }
	public AdmissionCrmSpecialRouteDataDto RouteData { get; set; } = new();
	public IReadOnlyList<string> EvidenceReferences { get; set; } = [];
	public string Reason { get; set; } = string.Empty;
}

public class EvaluateAdmissionCrmSpecialRouteDto
{
	public AdmissionCrmRouteEligibilityStatus Status { get; set; }
	public string Reason { get; set; } = string.Empty;
}

public class AdmissionCrmRouteAssignmentDto : EntityDto<Guid>
{
	public Guid LeadId { get; set; }
	public Guid RouteDefinitionId { get; set; }
	public int RouteDefinitionVersion { get; set; }
	public AdmissionCrmRouteType RouteType { get; set; }
	public AdmissionCrmRouteEligibilityStatus Status { get; set; }
	public AdmissionCrmSpecialRouteDataDto RouteData { get; set; } = new();
	public IReadOnlyList<string> EvidenceReferences { get; set; } = [];
	public Guid? PreviousAssignmentId { get; set; }
	public string AssignmentReason { get; set; } = string.Empty;
	public bool IsPrimary { get; set; }
	public DateTime AssignedAt { get; set; }
	public DateTime? EvaluatedAt { get; set; }
	public string? EvaluationReason { get; set; }
}

public class AdmissionCrmRouteImpactItemDto
{
	public string Area { get; set; } = string.Empty;
	public string Impact { get; set; } = string.Empty;
	public bool RequiresResolution { get; set; }
}

public class AdmissionCrmRouteImpactPreviewDto
{
	public Guid LeadId { get; set; }
	public AdmissionCrmRouteAssignmentDto? CurrentRoute { get; set; }
	public AdmissionCrmRouteDefinitionDto TargetRoute { get; set; } = new();
	public IReadOnlyList<AdmissionCrmRouteImpactItemDto> Impacts { get; set; } = [];
	public bool HasBlockingImpact { get; set; }
}

public class ImportAdmissionCrmRteAllotmentRowDto
{
	public Guid? LeadId { get; set; }
	public string? ApplicantReference { get; set; }
	public Guid RouteDefinitionId { get; set; }
	public AdmissionCrmSpecialRouteDataDto RouteData { get; set; } = new();
	public IReadOnlyList<string> EvidenceReferences { get; set; } = [];
}

public class ImportAdmissionCrmRteAllotmentsDto
{
	public IReadOnlyList<ImportAdmissionCrmRteAllotmentRowDto> Rows { get; set; } = [];
}

public class AdmissionCrmRteAllotmentImportItemResultDto
{
	public Guid? LeadId { get; set; }
	public string? ApplicantReference { get; set; }
	public string? AllotmentNumber { get; set; }
	public bool Success { get; set; }
	public string? ErrorCode { get; set; }
	public Guid? RouteAssignmentId { get; set; }
}

public class AdmissionCrmRteAllotmentImportResultDto
{
	public int Succeeded { get; set; }
	public int Failed { get; set; }
	public IReadOnlyList<AdmissionCrmRteAllotmentImportItemResultDto> Items { get; set; } = [];
}

public class RequestAdmissionCrmRouteAppealDto
{
	public AdmissionCrmRouteEligibilityStatus RequestedStatus { get; set; }
	public string Reason { get; set; } = string.Empty;
	public IReadOnlyList<string> EvidenceReferences { get; set; } = [];
}

public class DecideAdmissionCrmRouteAppealDto
{
	public bool Approved { get; set; }
	public string Reason { get; set; } = string.Empty;
}

public class AdmissionCrmRouteAppealDto : EntityDto<Guid>
{
	public Guid LeadId { get; set; }
	public Guid RouteAssignmentId { get; set; }
	public AdmissionCrmRouteEligibilityStatus OriginalStatus { get; set; }
	public AdmissionCrmRouteEligibilityStatus RequestedStatus { get; set; }
	public string Reason { get; set; } = string.Empty;
	public IReadOnlyList<string> EvidenceReferences { get; set; } = [];
	public AdmissionCrmRouteAppealStatus Status { get; set; }
	public DateTime RequestedAt { get; set; }
	public DateTime? DecidedAt { get; set; }
	public string? DecisionReason { get; set; }
}
