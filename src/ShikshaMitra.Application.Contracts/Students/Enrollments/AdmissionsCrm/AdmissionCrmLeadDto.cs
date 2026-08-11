using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadDto : EntityDto<Guid>
{
	public int ApplicantNumber { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid? AdmissionYearSetupId { get; set; }
	public int? AdmissionYearSetupVersion { get; set; }
	public Guid? StudentId { get; set; }
	public Guid? InquirySourceId { get; set; }
	public string? InquirySourceSnapshot { get; set; }
	public Guid? InquiryTypeId { get; set; }
	public string? InquiryTypeSnapshot { get; set; }
	public AdmissionInquiryChannel Channel { get; set; }
	public string? CampaignName { get; set; }
	public string? CampusBranchName { get; set; }
	public string? PreferredCommunicationLanguage { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	public string? ParentGuardianName { get; set; }
	public string? PrimaryContactNo { get; set; }
	public string? Email { get; set; }
	public string AdmissionIntent { get; set; } = string.Empty;
	public AdmissionCrmRouteType? IntendedRouteType { get; set; }
	public Guid? IntendedRouteDefinitionId { get; set; }
	public int? IntendedRouteDefinitionVersion { get; set; }
	public string? IntendedRouteNameSnapshot { get; set; }
	public Guid? TargetStandardId { get; set; }
	public string? DesiredClassName { get; set; }
	public AdmissionInquiryStage Stage { get; set; }
	public Guid? OwnerUserId { get; set; }
	public int LeadScore { get; set; }
	public AdmissionCrmLeadPriority Priority { get; set; }
	public string? RegistrationFormNumber { get; set; }
	public decimal? RegistrationFormAmount { get; set; }
	public DateTime? RegistrationFormIssuedAt { get; set; }
	public DateTimeOffset LastActivityTime { get; set; }
	public string? Notes { get; set; }
	public DateTime? ArchivedAt { get; set; }
	public string? ArchiveReason { get; set; }
	public DateTime? ReopenedAt { get; set; }
	public string? ReopenReason { get; set; }
	public Guid? MergedIntoLeadId { get; set; }
	public DateTime? MergedAt { get; set; }
	public string? MergeReason { get; set; }
	public string? DuplicateContinueReason { get; set; }
	public AdmissionCrmPipelineColumnKey? PipelineColumn { get; set; }
	public AdmissionCrmClosedOutcome? ClosedOutcome { get; set; }
	public AdmissionCrmApplicantStepKey? CurrentStep { get; set; }
	public string? NextAction { get; set; }
	public string? NextActionCode { get; set; }
	public int LikelyDuplicateCount { get; set; }
	public IReadOnlyList<AdmissionCrmLeadDuplicateDto> LikelyDuplicates { get; set; } = Array.Empty<AdmissionCrmLeadDuplicateDto>();
}
