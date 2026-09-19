using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionCycleDto : AuditedEntityDto<Guid>
{
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public string? Description { get; set; }
	public Guid AcademicYearId { get; set; }
	public string AcademicYearName { get; set; } = string.Empty;
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	public AdmissionCycleStatus Status { get; set; }
	public AdmissionCycleWindowStatus WindowStatus { get; set; }
	public bool IsAcceptingApplications { get; set; }
	public DateTime? PublishedAt { get; set; }
	public Guid? PublishedBy { get; set; }
	public DateTime? ClosedAt { get; set; }
	public Guid? ClosedBy { get; set; }
	public DateTime? ReopenedAt { get; set; }
	public Guid? ReopenedBy { get; set; }
	public DateTime? ArchivedAt { get; set; }
	public Guid? ArchivedBy { get; set; }
	public DateTime? ScheduledPublishAt { get; set; }
	public DateTime? ScheduledCloseAt { get; set; }
	public int LinkedOfferingCount { get; set; }
	public int ActiveOfferingCount { get; set; }
	public int ApplicationCount { get; set; }
}

public class CreateUpdateAdmissionCycleDto
{
	public string? ConcurrencyStamp { get; set; }
	[Required, StringLength(AdmissionCycleConsts.MaxNameLength)]
	public string Name { get; set; } = string.Empty;
	[Required, StringLength(AdmissionCycleConsts.MaxCodeLength)]
	public string Code { get; set; } = string.Empty;
	[StringLength(AdmissionCycleConsts.MaxDescriptionLength)]
	public string? Description { get; set; }
	public Guid AcademicYearId { get; set; }
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
}

public class AdmissionCycleListInput : PagedAndSortedResultRequestDto
{
	public string? Search { get; set; }
	public Guid? AcademicYearId { get; set; }
	public AdmissionCycleStatus? Status { get; set; }
	public AdmissionCycleWindowStatus? WindowStatus { get; set; }
}

public class AdmissionCycleActionDto
{
	public string? ConcurrencyStamp { get; set; }
	[StringLength(AdmissionCycleConsts.MaxReasonLength)]
	public string? Reason { get; set; }
	public bool ConfirmActiveOfferings { get; set; }
}

public class ReopenAdmissionCycleDto : AdmissionCycleActionDto
{
	public DateTime? NewEndDate { get; set; }
}

public class UpdateAdmissionCycleWindowDto : AdmissionCycleActionDto
{
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
}

public class ScheduleAdmissionCycleDto
{
	public string? ConcurrencyStamp { get; set; }
	public DateTime? PublishAt { get; set; }
	public DateTime? CloseAt { get; set; }
}

public class CloneAdmissionCycleDto : CreateUpdateAdmissionCycleDto
{
	public Guid SourceCycleId { get; set; }
}

public class AdmissionCycleValidationIssueDto
{
	public string Code { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
}

public class AdmissionCycleValidationDto
{
	public bool IsValid => Errors.Count == 0;
	public List<AdmissionCycleValidationIssueDto> Errors { get; set; } = [];
	public List<AdmissionCycleValidationIssueDto> Warnings { get; set; } = [];
}

public class AdmissionCycleImpactDto
{
	public Guid AdmissionCycleId { get; set; }
	public int LinkedOfferingCount { get; set; }
	public int ActiveOfferingCount { get; set; }
	public int ApplicationCount { get; set; }
	public int DraftApplicationCount { get; set; }
	public int SubmittedApplicationCount { get; set; }
	public bool RequiresActiveOfferingConfirmation => ActiveOfferingCount > 0;
}

public class AdmissionCycleHistoryDto : CreationAuditedEntityDto<Guid>
{
	public Guid AdmissionCycleId { get; set; }
	public AdmissionCycleHistoryAction Action { get; set; }
	public AdmissionCycleStatus? FromStatus { get; set; }
	public AdmissionCycleStatus? ToStatus { get; set; }
	public string Description { get; set; } = string.Empty;
	public string? Reason { get; set; }
	public DateTime OccurredAt { get; set; }
}
