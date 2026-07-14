using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class CreateAdmissionCrmRetentionPolicyDto
{
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public AdmissionCrmRetentionCategory Category { get; set; }
	public AdmissionCrmRetentionAction Action { get; set; }
	public int RetainDays { get; set; }
	public IReadOnlyDictionary<string, string?> Scope { get; set; } = new Dictionary<string, string?>();
	public string AuthorityNote { get; set; } = string.Empty;
}

public class AdmissionCrmRetentionPolicyDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public AdmissionCrmRetentionCategory Category { get; set; }
	public AdmissionCrmRetentionAction Action { get; set; }
	public int RetainDays { get; set; }
	public IReadOnlyDictionary<string, string?> Scope { get; set; } = new Dictionary<string, string?>();
	public string AuthorityNote { get; set; } = string.Empty;
	public AdmissionCrmGovernedConfigurationStatus Status { get; set; }
	public Guid? SubmittedByUserId { get; set; }
	public Guid? ApprovedByUserId { get; set; }
	public DateTime? PublishedAt { get; set; }
}

public class AdmissionCrmPrivacyPreviewDto
{
	public Guid LeadId { get; set; }
	public AdmissionCrmPrivacyRequestType Type { get; set; }
	public IReadOnlyList<string> RetentionBlockers { get; set; } = [];
	public IReadOnlyList<string> PlannedActions { get; set; } = [];
	public IReadOnlyList<Guid> ApplicablePolicyIds { get; set; } = [];
	public bool RequiresManualExecution { get; set; } = true;
}

public class CreateAdmissionCrmPrivacyRequestDto
{
	public AdmissionCrmPrivacyRequestType Type { get; set; }
	public string Reason { get; set; } = string.Empty;
}
public class DecideAdmissionCrmPrivacyRequestDto { public bool Approve { get; set; } public string Reason { get; set; } = string.Empty; }
public class CompleteAdmissionCrmPrivacyRequestDto { public string ExecutionReference { get; set; } = string.Empty; }

public class AdmissionCrmPrivacyRequestDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid LeadId { get; set; }
	public AdmissionCrmPrivacyRequestType Type { get; set; }
	public string Reason { get; set; } = string.Empty;
	public AdmissionCrmPrivacyPreviewDto Preview { get; set; } = new();
	public AdmissionCrmPrivacyRequestStatus Status { get; set; }
	public Guid RequestedByUserId { get; set; }
	public DateTime RequestedAt { get; set; }
	public Guid? DecidedByUserId { get; set; }
	public DateTime? DecidedAt { get; set; }
	public string? DecisionReason { get; set; }
	public string? CompletionReference { get; set; }
	public DateTime? CompletedAt { get; set; }
}

public class RequestAdmissionCrmExportDto
{
	public AdmissionCrmExportReportType ReportType { get; set; }
	public IReadOnlyDictionary<string, string?> Filters { get; set; } = new Dictionary<string, string?>();
	public int ExpiresInHours { get; set; } = 24;
}
public class AdmissionCrmExportRequestDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public AdmissionCrmExportReportType ReportType { get; set; }
	public string DefinitionVersion { get; set; } = string.Empty;
	public IReadOnlyDictionary<string, string?> Filters { get; set; } = new Dictionary<string, string?>();
	public string FileName { get; set; } = string.Empty;
	public AdmissionCrmExportStatus Status { get; set; }
	public Guid RequestedByUserId { get; set; }
	public DateTime RequestedAt { get; set; }
	public DateTime DataAsOf { get; set; }
	public DateTime ExpiresAt { get; set; }
	public int DownloadCount { get; set; }
	public DateTime? LastDownloadedAt { get; set; }
}
public class AdmissionCrmExportDownloadDto
{
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public string Base64Content { get; set; } = string.Empty;
	public DateTime ExpiresAt { get; set; }
}

public class CreateAdmissionCrmReportScheduleDto
{
	public string Name { get; set; } = string.Empty;
	public AdmissionCrmExportReportType ReportType { get; set; }
	public AdmissionCrmReportFrequency Frequency { get; set; }
	public string Destination { get; set; } = string.Empty;
	public IReadOnlyDictionary<string, string?> Filters { get; set; } = new Dictionary<string, string?>();
	public DateTime NextRunAt { get; set; }
}
public class AdmissionCrmReportScheduleDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string Name { get; set; } = string.Empty;
	public AdmissionCrmExportReportType ReportType { get; set; }
	public AdmissionCrmReportFrequency Frequency { get; set; }
	public string Destination { get; set; } = string.Empty;
	public IReadOnlyDictionary<string, string?> Filters { get; set; } = new Dictionary<string, string?>();
	public DateTime NextRunAt { get; set; }
	public bool IsActive { get; set; }
	public DateTime? LastRunAt { get; set; }
}

public class AdmissionCrmReportScheduleRunResultDto
{
	public Guid AcademicYearId { get; set; }
	public int EvaluatedCount { get; set; }
	public int GeneratedCount { get; set; }
	public int FailedCount { get; set; }
	public IReadOnlyList<Guid> ExportIds { get; set; } = [];
	public IReadOnlyList<Guid> FailedScheduleIds { get; set; } = [];
	public IReadOnlyDictionary<Guid, string> FailureDetails { get; set; } = new Dictionary<Guid, string>();
}

public class CreateAdmissionCrmWorkDelegationDto
{
	public Guid DelegatorUserId { get; set; }
	public Guid DelegateUserId { get; set; }
	public IReadOnlyList<AdmissionCrmOperationsQueueType> QueueTypes { get; set; } = [];
	public DateTime StartsAt { get; set; }
	public DateTime EndsAt { get; set; }
	public string Reason { get; set; } = string.Empty;
}
public class AdmissionCrmWorkDelegationDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid DelegatorUserId { get; set; }
	public Guid DelegateUserId { get; set; }
	public IReadOnlyList<AdmissionCrmOperationsQueueType> QueueTypes { get; set; } = [];
	public DateTime StartsAt { get; set; }
	public DateTime EndsAt { get; set; }
	public string Reason { get; set; } = string.Empty;
	public AdmissionCrmDelegationStatus Status { get; set; }
}

public class GetAdmissionCrmAuditInput : PagedAndSortedResultRequestDto
{
	public Guid AcademicYearId { get; set; }
	public Guid? LeadId { get; set; }
	public Guid? ActorUserId { get; set; }
	public AdmissionCrmTimelineEventType? EventType { get; set; }
	public DateTime? From { get; set; }
	public DateTime? To { get; set; }
}

public class AdmissionCrmForecastDto
{
	public Guid AcademicYearId { get; set; }
	public DateTime GeneratedAt { get; set; }
	public int HistoricalLeadCount { get; set; }
	public int CurrentOpenLeadCount { get; set; }
	public decimal ObservedOfferAcceptanceRate { get; set; }
	public decimal ObservedActivationRate { get; set; }
	public int ProjectedAdditionalAcceptedOffers { get; set; }
	public int ProjectedAdditionalActivations { get; set; }
	public string Confidence { get; set; } = string.Empty;
	public IReadOnlyList<string> Inputs { get; set; } = [];
	public IReadOnlyList<string> Limitations { get; set; } = [];
	public bool IsAdvisory { get; set; } = true;
}
