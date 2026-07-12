using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmInquirySourceUpsertDto
{
	[Required, MaxLength(AdmissionCrmConsts.MaxPlaybookCodeLength)]
	public string Code { get; set; } = string.Empty;
	[Required, MaxLength(AdmissionCrmConsts.MaxSourceSnapshotLength)]
	public string DisplayName { get; set; } = string.Empty;
	[MaxLength(AdmissionCrmConsts.MaxDescriptionLength)]
	public string? Description { get; set; }
	[MinLength(1)]
	public IReadOnlyList<AdmissionInquiryChannel> AllowedChannels { get; set; } = [];
	public int DisplayOrder { get; set; }
	public bool IsRoutingEligible { get; set; } = true;
}

public class AdmissionCrmInquirySourceDto : AdmissionCrmInquirySourceUpsertDto
{
	public Guid Id { get; set; }
	public Guid AcademicYearId { get; set; }
	public bool IsActive { get; set; }
	public string? DeactivationReason { get; set; }
}

public class AdmissionCrmCatalogStatusDto
{
	public bool IsActive { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? Reason { get; set; }
}

public class AdmissionCrmExamSlotUpsertDto
{
	public Guid? PlaybookId { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxPlaybookCodeLength)]
	public string? SlotTemplateCode { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxPlaybookCodeLength)]
	public string? ExamDefinitionCode { get; set; }
	[Required, MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string SlotName { get; set; } = string.Empty;
	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? Campus { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? RoomOrHall { get; set; }
	public DateTimeOffset StartDate { get; set; }
	public DateTimeOffset EndDate { get; set; }
	[Range(1, 10000)]
	public int StudentLimit { get; set; }
	[Range(1, 100000)]
	public int RoomNo { get; set; }
}

public class AdmissionCrmExamSlotDto : AdmissionCrmExamSlotUpsertDto
{
	public Guid Id { get; set; }
	public Guid AcademicYearId { get; set; }
	public bool IsActive { get; set; }
	public int ScheduledCount { get; set; }
	public int RemainingCapacity { get; set; }
}

public class AdmissionCrmDocumentTypeUpsertDto
{
	[Required, MaxLength(AdmissionCrmConsts.MaxPlaybookCodeLength)]
	public string Code { get; set; } = string.Empty;
	[Required, MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string Name { get; set; } = string.Empty;
	[Range(0.1, 25)]
	public decimal MaximumSizeMb { get; set; } = 5;
	[MinLength(1)]
	public IReadOnlyList<string> AcceptedExtensions { get; set; } = ["pdf", "jpg", "jpeg", "png"];
	public bool RequiresOriginal { get; set; }
	public bool ValidityDateRequired { get; set; }
	public int DisplayOrder { get; set; }
}

public class AdmissionCrmDocumentTypeDto : AdmissionCrmDocumentTypeUpsertDto
{
	public Guid Id { get; set; }
	public Guid AcademicYearId { get; set; }
	public bool IsActive { get; set; }
}

public interface IAdmissionCrmCatalogAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionCrmInquirySourceDto>> GetInquirySourcesAsync(Guid academicYearId, bool includeInactive = true);
	Task<AdmissionCrmInquirySourceDto> CreateInquirySourceAsync(Guid academicYearId, AdmissionCrmInquirySourceUpsertDto input);
	Task<AdmissionCrmInquirySourceDto> UpdateInquirySourceAsync(Guid id, AdmissionCrmInquirySourceUpsertDto input);
	Task<AdmissionCrmInquirySourceDto> SetInquirySourceStatusAsync(Guid id, AdmissionCrmCatalogStatusDto input);
	Task<PagedResultDto<AdmissionCrmExamSlotDto>> GetExamSlotsAsync(Guid academicYearId, DateTimeOffset? from = null, DateTimeOffset? to = null, bool includeInactive = false);
	Task<AdmissionCrmExamSlotDto> CreateExamSlotAsync(Guid academicYearId, AdmissionCrmExamSlotUpsertDto input);
	Task<AdmissionCrmExamSlotDto> UpdateExamSlotAsync(Guid id, AdmissionCrmExamSlotUpsertDto input);
	Task<AdmissionCrmExamSlotDto> SetExamSlotStatusAsync(Guid id, AdmissionCrmCatalogStatusDto input);
	Task<PagedResultDto<AdmissionCrmDocumentTypeDto>> GetDocumentTypesAsync(Guid academicYearId, bool includeInactive = true);
	Task<AdmissionCrmDocumentTypeDto> CreateDocumentTypeAsync(Guid academicYearId, AdmissionCrmDocumentTypeUpsertDto input);
	Task<AdmissionCrmDocumentTypeDto> UpdateDocumentTypeAsync(Guid id, AdmissionCrmDocumentTypeUpsertDto input);
	Task<AdmissionCrmDocumentTypeDto> SetDocumentTypeStatusAsync(Guid id, AdmissionCrmCatalogStatusDto input);
}
