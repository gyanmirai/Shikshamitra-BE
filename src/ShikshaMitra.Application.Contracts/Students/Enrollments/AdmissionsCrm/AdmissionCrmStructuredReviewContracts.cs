using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmReviewCriterionDto : EntityDto<Guid>
{
	public string Key { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public int MaximumScore { get; set; }
	public bool IsRequired { get; set; }
	public int DisplayOrder { get; set; }
}

public class AdmissionCrmReviewConfigurationDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public bool IsEnabled { get; set; }
	public string Title { get; set; } = string.Empty;
	public int RequiredCompletedReviews { get; set; }
	public bool RequireConflictDeclaration { get; set; }
	public int Version { get; set; }
	public List<AdmissionCrmReviewCriterionDto> Criteria { get; set; } = [];
}

public class UpdateAdmissionCrmReviewConfigurationDto
{
	public bool IsEnabled { get; set; }
	[Required, StringLength(AdmissionCrmConsts.MaxDescriptionLength)]
	public string Title { get; set; } = string.Empty;
	[Range(1, 10)]
	public int RequiredCompletedReviews { get; set; }
	public bool RequireConflictDeclaration { get; set; } = true;
	[MinLength(1), MaxLength(20)]
	public List<AdmissionCrmReviewCriterionInputDto> Criteria { get; set; } = [];
}

public class AdmissionCrmReviewCriterionInputDto
{
	[Required, StringLength(64)] public string Key { get; set; } = string.Empty;
	[Required, StringLength(AdmissionCrmConsts.MaxDescriptionLength)] public string Label { get; set; } = string.Empty;
	[Range(1, 100)] public int MaximumScore { get; set; }
	public bool IsRequired { get; set; }
}

public class AdmissionCrmStructuredReviewDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid LeadId { get; set; }
	public int ConfigurationVersion { get; set; }
	public string Title { get; set; } = string.Empty;
	public int RequiredCompletedReviews { get; set; }
	public int SubmittedReviewCount { get; set; }
	public bool RequireConflictDeclaration { get; set; }
	public AdmissionCrmStructuredReviewStatus Status { get; set; }
	public DateTime StartedAt { get; set; }
	public DateTime? ReadyAt { get; set; }
	public DateTime? ClosedAt { get; set; }
	public string? ClosureReason { get; set; }
	public List<AdmissionCrmReviewCriterionDto> Criteria { get; set; } = [];
	public List<AdmissionCrmReviewAssignmentDto> Assignments { get; set; } = [];
}

public class AdmissionCrmReviewAssignmentDto : EntityDto<Guid>
{
	public Guid ReviewId { get; set; }
	public Guid LeadId { get; set; }
	public string ReviewTitle { get; set; } = string.Empty;
	public AdmissionCrmStructuredReviewStatus ReviewStatus { get; set; }
	public Guid ReviewerUserId { get; set; }
	public bool IsCurrentReviewer { get; set; }
	public DateTime DueAt { get; set; }
	public AdmissionCrmReviewAssignmentStatus Status { get; set; }
	public DateTime AssignedAt { get; set; }
	public bool? HasDeclaredConflict { get; set; }
	public string? ConflictReason { get; set; }
	public DateTime? ConflictDeclaredAt { get; set; }
	public AdmissionCrmReviewRecommendation? Recommendation { get; set; }
	public string? ConfidentialRecommendation { get; set; }
	public Dictionary<string, int?>? Scores { get; set; }
	public DateTime? SubmittedAt { get; set; }
	public List<AdmissionCrmReviewCriterionDto> Criteria { get; set; } = [];
}

public class AssignAdmissionCrmReviewerDto
{
	public Guid ReviewerUserId { get; set; }
	public DateTime DueAt { get; set; }
}

public class DeclareAdmissionCrmReviewConflictDto
{
	public bool HasConflict { get; set; }
	[StringLength(AdmissionCrmConsts.MaxNotesLength)] public string? Reason { get; set; }
}

public class SubmitAdmissionCrmStructuredReviewDto
{
	public AdmissionCrmReviewRecommendation Recommendation { get; set; }
	[Required, StringLength(AdmissionCrmConsts.MaxNotesLength)]
	public string ConfidentialRecommendation { get; set; } = string.Empty;
	public Dictionary<string, int?> Scores { get; set; } = [];
}

public class CloseAdmissionCrmStructuredReviewDto
{
	[Required, StringLength(AdmissionCrmConsts.MaxNotesLength)]
	public string Reason { get; set; } = string.Empty;
}
