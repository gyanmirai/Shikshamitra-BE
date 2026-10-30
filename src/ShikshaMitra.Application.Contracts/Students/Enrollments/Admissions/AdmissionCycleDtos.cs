using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Timing;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionCycleDto : AuditedEntityDto<Guid>
{
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public string? Description { get; set; }
	public Guid AcademicYearId { get; set; }
	public string AcademicYearName { get; set; } = string.Empty;
	[DisableDateTimeNormalization, JsonConverter(typeof(AdmissionCalendarDateJsonConverter))]
	public DateTime StartDate { get; set; }
	[DisableDateTimeNormalization, JsonConverter(typeof(AdmissionCalendarDateJsonConverter))]
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
	[StringLength(AdmissionCycleConsts.MaxCodeLength)]
	public string? Code { get; set; }
	[StringLength(AdmissionCycleConsts.MaxDescriptionLength)]
	public string? Description { get; set; }
	public Guid AcademicYearId { get; set; }
	[DisableDateTimeNormalization, JsonConverter(typeof(AdmissionCalendarDateJsonConverter))]
	public DateTime StartDate { get; set; }
	[DisableDateTimeNormalization, JsonConverter(typeof(AdmissionCalendarDateJsonConverter))]
	public DateTime EndDate { get; set; }
}

public class AdmissionCycleListInput : PagedAndSortedResultRequestDto
{
	public string? Search { get; set; }
	public Guid? AcademicYearId { get; set; }
	public AdmissionCycleStatus? Status { get; set; }
	public AdmissionCycleWindowStatus? WindowStatus { get; set; }
	public string? ColumnFilters { get; set; }
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
	[DisableDateTimeNormalization, JsonConverter(typeof(AdmissionNullableCalendarDateJsonConverter))]
	public DateTime? NewEndDate { get; set; }
}

public class UpdateAdmissionCycleWindowDto : AdmissionCycleActionDto
{
	[DisableDateTimeNormalization, JsonConverter(typeof(AdmissionCalendarDateJsonConverter))]
	public DateTime StartDate { get; set; }
	[DisableDateTimeNormalization, JsonConverter(typeof(AdmissionCalendarDateJsonConverter))]
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

public class AdmissionCycleCapacitySummaryDto
{
	public Guid AdmissionCycleId { get; set; }
	public int OfferingCount { get; set; }
	public int ActiveOfferingCount { get; set; }
	public int SeatManagedOfferingCount { get; set; }
	public int ConfiguredOfferingCount { get; set; }
	public int UnconfiguredOfferingCount { get; set; }
	public int NotSeatManagedOfferingCount => OfferingCount - SeatManagedOfferingCount;
	public int TotalCapacity { get; set; }
	public int ReservedCount { get; set; }
	public int ConfirmedCount { get; set; }
	public int WaitlistedCount { get; set; }
	public int OccupiedCount => ReservedCount + ConfirmedCount;
	public int RemainingCapacity { get; set; }
	public int OverCapacityOfferingCount { get; set; }
	public List<AdmissionCycleOfferingCapacityDto> Offerings { get; set; } = [];
}

public class AdmissionCycleOfferingCapacityDto
{
	public Guid AdmissionOfferingId { get; set; }
	public string OfferingName { get; set; } = string.Empty;
	public string OfferingCode { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public bool IsSeatManaged { get; set; }
	public bool IsConfigured { get; set; }
	public int? Capacity { get; set; }
	public int ReservedCount { get; set; }
	public int ConfirmedCount { get; set; }
	public int WaitlistedCount { get; set; }
	public int? RemainingCapacity { get; set; }
	public bool IsOverCapacity { get; set; }
	public string? PoolConcurrencyStamp { get; set; }
}

public class UpdateAdmissionCycleOfferingCapacityDto
{
	public int Capacity { get; set; }
	public string? PoolConcurrencyStamp { get; set; }
	[StringLength(AdmissionCycleConsts.MaxReasonLength)]
	public string Reason { get; set; } = string.Empty;
}
