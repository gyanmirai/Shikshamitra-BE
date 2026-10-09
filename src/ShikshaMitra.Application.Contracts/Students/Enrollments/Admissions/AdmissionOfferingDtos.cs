using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionOfferingListDto : EntityDto<Guid>
{
	public string Name { get; set; } = string.Empty;
	public Guid AdmissionCycleId { get; set; }
	public string AdmissionCycleName { get; set; } = string.Empty;
	public Guid? CampusId { get; set; }
	public string? CampusName { get; set; }
	public Guid BoardId { get; set; }
	public string BoardName { get; set; } = string.Empty;
	public Guid MediumId { get; set; }
	public string MediumName { get; set; } = string.Empty;
	public Guid StageId { get; set; }
	public string StageName { get; set; } = string.Empty;
	public Guid StandardId { get; set; }
	public string StandardName { get; set; } = string.Empty;
	public AdmissionTrack Track { get; set; } = AdmissionTrack.General;
	public AdmissionEntryMode EntryMode { get; set; } = AdmissionEntryMode.Inquiry;
	public string Code { get; set; } = string.Empty;
	public DateTimeOffset EffectiveOpenDate { get; set; }
	public DateTimeOffset EffectiveCloseDate { get; set; }
	public string? WorkflowName { get; set; }
	public int? WorkflowVersion { get; set; }
	public bool IsActive { get; set; }
	public AdmissionOfferingAvailabilityStatus AvailabilityStatus { get; set; }
	public AdmissionOfferingConfigurationStatus ConfigurationStatus { get; set; }
	public bool CanActivate => ConfigurationStatus == AdmissionOfferingConfigurationStatus.Ready;
	public int ApplicationCount { get; set; }
	public bool CanEditIdentity => ApplicationCount == 0;
	public bool CanDelete => ApplicationCount == 0;
}

public class AdmissionOfferingDto : AdmissionOfferingListDto
{
	public AdmissionClassCapacityDto? ClassCapacity { get; set; }
	public bool UseCycleDates { get; set; }
	public DateTimeOffset? OpenDate { get; set; }
	public DateTimeOffset? CloseDate { get; set; }
	public Guid? WorkflowDefinitionId { get; set; }
	public Guid? InquiryFormDefinitionId { get; set; }
	public Guid? ApplicationFormDefinitionId { get; set; }
	public Guid? InquiryIntakeProfileId { get; set; }
	public Guid? CounsellorAssignmentProfileId { get; set; }
	public string? InquiryFormName { get; set; }
	public int? InquiryFormVersion { get; set; }
	public string? ApplicationFormName { get; set; }
	public int? ApplicationFormVersion { get; set; }
	public AdmissionOfferingConfigurationValidationDto Configuration { get; set; } = new();
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public DateTime? LastModificationTime { get; set; }
	public Guid? LastModifierId { get; set; }
}

public abstract class AdmissionOfferingWriteDto
{
	public string? Name { get; set; }
	public AdmissionTrack Track { get; set; } = AdmissionTrack.General;
	public AdmissionEntryMode EntryMode { get; set; } = AdmissionEntryMode.Inquiry;
	public Guid? CampusId { get; set; }
	public Guid BoardId { get; set; }
	public Guid MediumId { get; set; }
	public Guid StageId { get; set; }
	public Guid StandardId { get; set; }
	public string Code { get; set; } = string.Empty;
	public bool UseCycleDates { get; set; }
	public DateTimeOffset? OpenDate { get; set; }
	public DateTimeOffset? CloseDate { get; set; }
	public Guid? WorkflowDefinitionId { get; set; }
	public Guid? InquiryFormDefinitionId { get; set; }
	public Guid? ApplicationFormDefinitionId { get; set; }
	public Guid? InquiryIntakeProfileId { get; set; }
	public Guid? CounsellorAssignmentProfileId { get; set; }
	public bool IsActive { get; set; } = true;
}

public class CreateAdmissionOfferingDto : AdmissionOfferingWriteDto;

public class UpdateAdmissionOfferingDto : AdmissionOfferingWriteDto
{
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class ValidateAdmissionOfferingDto : AdmissionOfferingWriteDto
{
	public Guid AdmissionCycleId { get; set; }
	public Guid? ExcludingOfferingId { get; set; }
}

public class CloneAdmissionOfferingDto
{
	public string? Name { get; set; }
	public AdmissionTrack? Track { get; set; }
	public AdmissionEntryMode? EntryMode { get; set; }
	public Guid AdmissionCycleId { get; set; }
	public Guid? CampusId { get; set; }
	public Guid BoardId { get; set; }
	public Guid MediumId { get; set; }
	public Guid StageId { get; set; }
	public Guid StandardId { get; set; }
	public string Code { get; set; } = string.Empty;
	public bool? IsActive { get; set; }
}

public class AdmissionOfferingValidationErrorDto
{
	public string Code { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
}

public class AdmissionOfferingConfigurationValidationDto
{
	public bool IsValid { get; set; }
	public AdmissionOfferingConfigurationStatus Status { get; set; }
	public List<AdmissionOfferingValidationErrorDto> Errors { get; set; } = [];
}

public class AdmissionOfferingAvailabilityDto
{
	public AdmissionClassCapacityDto? ClassCapacity { get; set; }
	public bool IsAvailable { get; set; }
	public AdmissionOfferingAvailabilityStatus Status { get; set; }
	public bool CanCreateInquiry { get; set; }
	public bool CanStartApplication { get; set; }
	public string? ReasonCode { get; set; }
	public string? ReasonMessage { get; set; }
}

public class AdmissionOfferingListInput : PagedAndSortedResultRequestDto
{
	public Guid? AdmissionCycleId { get; set; }
	public AdmissionTrack? Track { get; set; }
	public AdmissionEntryMode? EntryMode { get; set; }
	public Guid? CampusId { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public Guid? StageId { get; set; }
	public Guid? StandardId { get; set; }
	public List<Guid> StandardIds { get; set; } = [];
	public bool? IsActive { get; set; }
	public AdmissionOfferingAvailabilityStatus? AvailabilityStatus { get; set; }
	public AdmissionOfferingConfigurationStatus? ConfigurationStatus { get; set; }
	public string? Search { get; set; }
}

public class BulkAdmissionOfferingItemDto
{
	public string? Name { get; set; }
	public Guid StandardId { get; set; }
	public string Code { get; set; } = string.Empty;
}

public class BulkCreateAdmissionOfferingsDto
{
	public AdmissionTrack Track { get; set; } = AdmissionTrack.General;
	public AdmissionEntryMode EntryMode { get; set; } = AdmissionEntryMode.Inquiry;
	public Guid? CampusId { get; set; }
	public Guid BoardId { get; set; }
	public Guid MediumId { get; set; }
	public Guid? StageId { get; set; }
	public bool UseCycleDates { get; set; }
	public DateTimeOffset? OpenDate { get; set; }
	public DateTimeOffset? CloseDate { get; set; }
	public Guid? WorkflowDefinitionId { get; set; }
	public Guid? InquiryFormDefinitionId { get; set; }
	public Guid? ApplicationFormDefinitionId { get; set; }
	public Guid? InquiryIntakeProfileId { get; set; }
	public Guid? CounsellorAssignmentProfileId { get; set; }
	public bool IsActive { get; set; } = true;
	public bool PreviewOnly { get; set; } = true;
	public List<BulkAdmissionOfferingItemDto> Items { get; set; } = [];
}

public class BulkAdmissionOfferingResultItemDto
{
	public Guid StandardId { get; set; }
	public string Code { get; set; } = string.Empty;
	public Guid? OfferingId { get; set; }
	public bool IsValid { get; set; }
	public bool CanSaveInactive { get; set; }
	public bool CanActivate { get; set; }
	public List<AdmissionOfferingValidationErrorDto> Errors { get; set; } = [];
}

public class BulkAdmissionOfferingResultDto
{
	public bool IsPreview { get; set; }
	public bool CanCommit { get; set; }
	public int CreatedCount { get; set; }
	public List<BulkAdmissionOfferingResultItemDto> Items { get; set; } = [];
}
