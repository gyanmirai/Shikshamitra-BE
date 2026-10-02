using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowStageDto
{
	public Guid Id { get; set; }
	public Guid WorkflowDefinitionId { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public AdmissionWorkflowStageType StageType { get; set; }
	public int DisplayOrder { get; set; }
	public bool IsRequired { get; set; }
	public bool AllowManualSkip { get; set; }
	public bool IsEntryStage { get; set; }
	public bool IsTerminalStage { get; set; }
	public string? ConfigurationJson { get; set; }
}

public class AdmissionWorkflowTransitionDto
{
	public Guid Id { get; set; }
	public Guid WorkflowDefinitionId { get; set; }
	public Guid FromStageId { get; set; }
	public Guid ToStageId { get; set; }
	public int Priority { get; set; }
	public string? ConditionRuleJson { get; set; }
	public bool IsDefault { get; set; }
}

public class AdmissionWorkflowDto : FullAuditedEntityDto<Guid>
{
	public string Name { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public string? Description { get; set; }
	public Guid? DefaultInquiryFormDefinitionId { get; set; }
	public int Version { get; set; }
	public AdmissionWorkflowStatus Status { get; set; }
	public Guid? ParentVersionId { get; set; }
	public DateTime? PublishedAt { get; set; }
	public Guid? PublishedBy { get; set; }
	public Guid? OwnerUserId { get; set; }
	public DateTime? ValidatedAt { get; set; }
	public Guid? ValidatedBy { get; set; }
	public DateTime? SubmittedAt { get; set; }
	public Guid? SubmittedBy { get; set; }
	public Guid? AssignedApproverId { get; set; }
	public DateTime? ApprovedAt { get; set; }
	public Guid? ApprovedBy { get; set; }
	public DateTime? ScheduledPublishAt { get; set; }
	public string? ChangeReason { get; set; }
	public Guid? RollbackSourceVersionId { get; set; }
	public int OfferingCount { get; set; }
	public int CampusCount { get; set; }
	public int ApplicationCount { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public List<AdmissionWorkflowStageDto> Stages { get; set; } = [];
	public List<AdmissionWorkflowTransitionDto> Transitions { get; set; } = [];
}

public class CreateUpdateAdmissionWorkflowStageDto
{
	public Guid? Id { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public AdmissionWorkflowStageType StageType { get; set; }
	public int DisplayOrder { get; set; }
	public bool IsRequired { get; set; } = true;
	public bool AllowManualSkip { get; set; }
	public bool IsEntryStage { get; set; }
	public bool IsTerminalStage { get; set; }
	public string? ConfigurationJson { get; set; }
}

public class CreateUpdateAdmissionWorkflowTransitionDto
{
	public Guid? Id { get; set; }
	public Guid FromStageId { get; set; }
	public Guid ToStageId { get; set; }
	public int Priority { get; set; }
	public string? ConditionRuleJson { get; set; }
	public bool IsDefault { get; set; }
}

public class CreateUpdateAdmissionWorkflowDto
{
	public string Name { get; set; } = string.Empty;
	public string? Code { get; set; }
	public string? Description { get; set; }
	public Guid? DefaultInquiryFormDefinitionId { get; set; }
	public string? ConcurrencyStamp { get; set; }
	public List<CreateUpdateAdmissionWorkflowStageDto> Stages { get; set; } = [];
	public List<CreateUpdateAdmissionWorkflowTransitionDto> Transitions { get; set; } = [];
}

public class AdmissionWorkflowListInput : PagedAndSortedResultRequestDto
{
	public string? Search { get; set; }
	public AdmissionWorkflowStatus? Status { get; set; }
	public int? Version { get; set; }
	public bool? IsUsed { get; set; }
	public DateTime? CreatedFrom { get; set; }
	public DateTime? CreatedTo { get; set; }
}

public class AdmissionWorkflowValidationResultDto
{
	public bool IsValid => Issues.TrueForAll(issue => issue.Severity != AdmissionWorkflowValidationSeverity.Error);
	public List<AdmissionWorkflowValidationIssueDto> Issues { get; set; } = [];
	public List<string> Errors => Issues
		.FindAll(issue => issue.Severity == AdmissionWorkflowValidationSeverity.Error)
		.ConvertAll(issue => issue.Code);
	public List<string> Warnings => Issues
		.FindAll(issue => issue.Severity == AdmissionWorkflowValidationSeverity.Warning)
		.ConvertAll(issue => issue.Code);
}

public class AdmissionWorkflowValidationIssueDto
{
	public string Code { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public AdmissionWorkflowValidationSeverity Severity { get; set; }
	public Guid? StageId { get; set; }
	public Guid? TransitionId { get; set; }
}

public class CloneAdmissionWorkflowDto
{
	public string Name { get; set; } = string.Empty;
	public string? Code { get; set; }
	public string? Description { get; set; }
}

public class SubmitAdmissionWorkflowDto
{
	public Guid ApproverUserId { get; set; }
	public string ChangeReason { get; set; } = string.Empty;
	public string? ConcurrencyStamp { get; set; }
}

public class DecideAdmissionWorkflowDto
{
	public string? Reason { get; set; }
	public string? ConcurrencyStamp { get; set; }
}

public class PublishAdmissionWorkflowDto
{
	public string ChangeReason { get; set; } = string.Empty;
	public DateTime? EffectiveAt { get; set; }
	public string? ConcurrencyStamp { get; set; }
}

public class RollbackAdmissionWorkflowDto
{
	public string ChangeReason { get; set; } = string.Empty;
}

public class AdmissionWorkflowGovernanceSettingDto
{
	public bool MakerCheckerEnabled { get; set; }
	public bool InAppNotificationsEnabled { get; set; }
	public bool EmailNotificationsEnabled { get; set; }
}

public class UpdateAdmissionWorkflowGovernanceSettingDto : AdmissionWorkflowGovernanceSettingDto
{
}

public class AdmissionWorkflowApproverDto
{
	public Guid Id { get; set; }
	public string UserName { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
}

public class AdmissionWorkflowGovernanceHistoryDto
{
	public Guid Id { get; set; }
	public AdmissionWorkflowGovernanceAction Action { get; set; }
	public AdmissionWorkflowStatus? FromStatus { get; set; }
	public AdmissionWorkflowStatus ToStatus { get; set; }
	public Guid? ActorUserId { get; set; }
	public string? ActorName { get; set; }
	public Guid? AssignedApproverId { get; set; }
	public string? AssignedApproverName { get; set; }
	public string? Reason { get; set; }
	public DateTime? EffectiveAt { get; set; }
	public DateTime OccurredAt { get; set; }
}

public class AdmissionWorkflowNotificationDto
{
	public Guid Id { get; set; }
	public Guid WorkflowDefinitionId { get; set; }
	public AdmissionWorkflowNotificationType Type { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public bool IsRead { get; set; }
	public DateTime? ReadAt { get; set; }
	public DateTime CreationTime { get; set; }
}

public class AdmissionWorkflowUsageDto
{
	public Guid WorkflowDefinitionId { get; set; }
	public int OfferingCount { get; set; }
	public int CampusCount { get; set; }
	public int ApplicationCount { get; set; }
	public bool CanDelete { get; set; }
	public List<AdmissionWorkflowOfferingUsageDto> Offerings { get; set; } = [];
	public AdmissionWorkflowRuntimeAnalyticsDto RuntimeAnalytics { get; set; } = new();
}

public class AdmissionWorkflowOfferingUsageDto
{
	public Guid Id { get; set; }
	public Guid AdmissionCycleId { get; set; }
	public Guid? CampusId { get; set; }
	public string AdmissionCycleName { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public bool IsActive { get; set; }
}

public class AdmissionWorkflowRuntimeAnalyticsDto
{
	public int StartedCount { get; set; }
	public int ActiveCount { get; set; }
	public int CompletedCount { get; set; }
	public decimal CompletionRatePercent { get; set; }
	public decimal? AverageCompletionHours { get; set; }
	public List<AdmissionWorkflowStageAnalyticsDto> Stages { get; set; } = [];
}

public class AdmissionWorkflowStageAnalyticsDto
{
	public Guid DefinitionStageId { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public AdmissionWorkflowStageType StageType { get; set; }
	public int DisplayOrder { get; set; }
	public int EnteredCount { get; set; }
	public int ActiveCount { get; set; }
	public int CompletedCount { get; set; }
	public int SkippedCount { get; set; }
	public decimal CompletionRatePercent { get; set; }
	public decimal? AverageDurationHours { get; set; }
	public DateTime? OldestActiveEnteredAt { get; set; }
}

public class AdmissionWorkflowStageTypeMetadataDto
{
	public AdmissionWorkflowStageType Type { get; set; }
	public string DisplayName { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Icon { get; set; } = string.Empty;
	public string DefaultColor { get; set; } = string.Empty;
	public IReadOnlyList<string> SupportedConfiguration { get; set; } = [];
	public string RuntimeHandler { get; set; } = string.Empty;
	public string NextRequiredAction { get; set; } = string.Empty;
	public bool CanBeEntryStage { get; set; }
	public bool CanBeTerminalStage { get; set; }
	public bool SupportsFormAttachment { get; set; }
	public bool SupportsConditions { get; set; }
	public bool IsRuntimeReady { get; set; }
	public bool IsSelectable { get; set; }
	public bool IsDeprecated { get; set; }
	public string? ReplacementGuidance { get; set; }
}
