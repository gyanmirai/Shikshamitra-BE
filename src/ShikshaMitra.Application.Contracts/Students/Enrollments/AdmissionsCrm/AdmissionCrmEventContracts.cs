using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmEventDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string Title { get; set; } = string.Empty;
	public AdmissionCrmEventType EventType { get; set; }
	public DateTime StartsAt { get; set; }
	public DateTime EndsAt { get; set; }
	public string Location { get; set; } = string.Empty;
	public int Capacity { get; set; }
	public int ReservedCount { get; set; }
	public int AvailableCount { get; set; }
	public Guid? OwnerUserId { get; set; }
	public string? Instructions { get; set; }
	public bool AllowFamilySelfScheduling { get; set; }
	public AdmissionCrmEventStatus Status { get; set; }
	public DateTime? PublishedAt { get; set; }
	public DateTime? CancelledAt { get; set; }
	public string? CancellationReason { get; set; }
	public List<AdmissionCrmEventRegistrationDto> Registrations { get; set; } = [];
}

public class AdmissionCrmEventRegistrationDto : EntityDto<Guid>
{
	public Guid LeadId { get; set; }
	public string ApplicantName { get; set; } = string.Empty;
	public string? ParentGuardianName { get; set; }
	public string? PrimaryContactNo { get; set; }
	public AdmissionCrmEventRegistrationStatus Status { get; set; }
	public DateTime ReservedAt { get; set; }
	public DateTime? CheckedInAt { get; set; }
	public DateTime? CancelledAt { get; set; }
	public DateTime? NoShowRecordedAt { get; set; }
	public string? Notes { get; set; }
	public string? CancellationReason { get; set; }
}

public class GetAdmissionCrmEventListInput : PagedAndSortedResultRequestDto
{
	public string? FilterText { get; set; }
	public AdmissionCrmEventType? EventType { get; set; }
	public AdmissionCrmEventStatus? Status { get; set; }
	public DateTime? StartsFrom { get; set; }
	public DateTime? StartsUntil { get; set; }
}

public class CreateUpdateAdmissionCrmEventDto
{
	[Required, StringLength(AdmissionCrmConsts.MaxDescriptionLength)]
	public string Title { get; set; } = string.Empty;
	public AdmissionCrmEventType EventType { get; set; }
	public DateTime StartsAt { get; set; }
	public DateTime EndsAt { get; set; }
	[Required, StringLength(AdmissionCrmConsts.MaxDescriptionLength)]
	public string Location { get; set; } = string.Empty;
	[Range(1, 5000)]
	public int Capacity { get; set; }
	public Guid? OwnerUserId { get; set; }
	[StringLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? Instructions { get; set; }
	public bool AllowFamilySelfScheduling { get; set; }
}

public class AdmissionCrmPublicEventListDto
{
	public List<AdmissionCrmPublicEventDto> Events { get; set; } = [];
}

public class AdmissionCrmPublicEventDto : EntityDto<Guid>
{
	public string Title { get; set; } = string.Empty;
	public AdmissionCrmEventType EventType { get; set; }
	public DateTime StartsAt { get; set; }
	public DateTime EndsAt { get; set; }
	public string Location { get; set; } = string.Empty;
	public string? Instructions { get; set; }
	public int AvailableCount { get; set; }
	public Guid? RegistrationId { get; set; }
	public AdmissionCrmEventRegistrationStatus? RegistrationStatus { get; set; }
}

public class ReserveAdmissionCrmPublicEventDto
{
	[JsonIgnore]
	public string AccessSessionToken { get; set; } = string.Empty;
}

public class CancelAdmissionCrmPublicEventRegistrationDto
{
	[JsonIgnore]
	public string AccessSessionToken { get; set; } = string.Empty;
	[StringLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? Reason { get; set; }
}

public class ReserveAdmissionCrmEventRegistrationDto
{
	public Guid LeadId { get; set; }
	[StringLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? Notes { get; set; }
}

public class CancelAdmissionCrmEventDto
{
	[Required, StringLength(AdmissionCrmConsts.MaxNotesLength)]
	public string Reason { get; set; } = string.Empty;
}

public class CancelAdmissionCrmEventRegistrationDto
{
	[StringLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? Reason { get; set; }
}
