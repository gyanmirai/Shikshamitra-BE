using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmOperationsQueueSummaryDto
{
	public Guid AcademicYearId { get; set; }
	public DateTime GeneratedAt { get; set; }
	public IReadOnlyList<AdmissionCrmOperationsQueueCountDto> Queues { get; set; } = [];
}

public class AdmissionCrmOperationsQueueCountDto
{
	public AdmissionCrmOperationsQueueType QueueType { get; set; }
	public string Label { get; set; } = string.Empty;
	public int Count { get; set; }
	public int UrgentCount { get; set; }
}

public class AdmissionCrmOperationsQueueDto
{
	public Guid AcademicYearId { get; set; }
	public AdmissionCrmOperationsQueueType QueueType { get; set; }
	public string Label { get; set; } = string.Empty;
	public DateTime GeneratedAt { get; set; }
	public int TotalCount { get; set; }
	public IReadOnlyList<AdmissionCrmOperationsQueueItemDto> Items { get; set; } = [];
}

public class GetAdmissionCrmOperationsQueueInput : PagedResultRequestDto
{
	public string? Search { get; set; }
	public Guid? OwnerUserId { get; set; }
	public Guid? AssignedToUserId { get; set; }
	public bool? UrgentOnly { get; set; }
	public int? MinimumAgeDays { get; set; }
	public DateTimeOffset? DueBefore { get; set; }
}

public class CreateAdmissionCrmOperationsSavedViewDto
{
	public string Name { get; set; } = string.Empty;
	public AdmissionCrmOperationsQueueType QueueType { get; set; }
	public GetAdmissionCrmOperationsQueueInput Filters { get; set; } = new();
	public bool IsShared { get; set; }
}

public class AdmissionCrmOperationsSavedViewDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid OwnerUserId { get; set; }
	public string Name { get; set; } = string.Empty;
	public AdmissionCrmOperationsQueueType QueueType { get; set; }
	public GetAdmissionCrmOperationsQueueInput Filters { get; set; } = new();
	public bool IsShared { get; set; }
}

public class AdmissionCrmOperationsQueueItemDto
{
	public string Key { get; set; } = string.Empty;
	public Guid LeadId { get; set; }
	public Guid? ExamRecordId { get; set; }
	public Guid? RegistrationApplicationId { get; set; }
	public Guid? DocumentId { get; set; }
	public Guid? ChecklistAssignmentId { get; set; }
	public Guid? ServiceHandoffId { get; set; }
	public string ApplicantName { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string? Details { get; set; }
	public string Status { get; set; } = string.Empty;
	public DateTimeOffset? DueAt { get; set; }
	public int AgeDays { get; set; }
	public bool IsUrgent { get; set; }
	public Guid? OwnerUserId { get; set; }
}

public class AdmissionCrmAdmissionHandoffPackageDto
{
	public Guid AcademicYearId { get; set; }
	public Guid LeadId { get; set; }
	public Guid RegistrationApplicationId { get; set; }
	public Guid ChecklistAssignmentId { get; set; }
	public string ApplicantName { get; set; } = string.Empty;
	public string FormNumber { get; set; } = string.Empty;
	public AdmissionInquiryStage LeadStage { get; set; }
	public AdmissionCrmRegistrationStatus RegistrationStatus { get; set; }
	public DateTime RegistrationVerifiedAt { get; set; }
	public DateTime ReadinessConfirmedAt { get; set; }
	public bool ReadinessOverridden { get; set; }
	public string? ReadinessReason { get; set; }
	public AdmissionCrmEligibilityDecision? EligibilityDecision { get; set; }
	public int? MeritRank { get; set; }
	public string? PaymentReference { get; set; }
	public AdmissionCrmRegistrationFeeStatus FeeStatus { get; set; }
	public IReadOnlyDictionary<string, string?> ApplicantProfile { get; set; } = new Dictionary<string, string?>();
	public IReadOnlyList<AdmissionCrmAdmissionHandoffDocumentDto> Documents { get; set; } = [];
	public DateTime GeneratedAt { get; set; }
}

public class AdmissionCrmAdmissionHandoffDocumentDto
{
	public string RequirementKey { get; set; } = string.Empty;
	public int Version { get; set; }
	public AdmissionCrmApplicantDocumentStatus Status { get; set; }
	public string? FileName { get; set; }
	public DateTime? ValidUntil { get; set; }
	public DateTime? DecisionAt { get; set; }
	public DateTime? OriginalSeenAt { get; set; }
}
