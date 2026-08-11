using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class PreviewAdmissionCrmGeneratedDocumentDto
{
	public AdmissionCrmGeneratedDocumentType DocumentType { get; set; }

	[MaxLength(8)]
	public string Language { get; set; } = "en";
}

public class IssueAdmissionCrmGeneratedDocumentDto : PreviewAdmissionCrmGeneratedDocumentDto
{
	[MaxLength(2000)]
	public string? Reason { get; set; }
}

public class SendAdmissionCrmGeneratedDocumentEmailDto : IssueAdmissionCrmGeneratedDocumentDto;

public class AdmissionCrmGeneratedDocumentDeliveryDto
{
	public required AdmissionCrmGeneratedDocumentDto Document { get; set; }
	public AdmissionCrmMessageDeliveryLogDto? Delivery { get; set; }
	public string? DeliveryError { get; set; }
}

public class AdmissionCrmGeneratedDocumentDto
{
	public Guid LeadId { get; set; }
	public Guid AcademicYearId { get; set; }
	public AdmissionCrmGeneratedDocumentType DocumentType { get; set; }
	public string DocumentTypeName { get; set; } = string.Empty;
	public string Language { get; set; } = "en";
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = "application/pdf";
	public byte[] Content { get; set; } = [];
	public bool IsPreview { get; set; }
	public DateTimeOffset GeneratedAt { get; set; }
	public Guid? TimelineEventId { get; set; }
}

public class AdmissionCrmGeneratedDocumentHistoryDto
{
	public Guid TimelineEventId { get; set; }
	public Guid LeadId { get; set; }
	public AdmissionCrmGeneratedDocumentType DocumentType { get; set; }
	public string DocumentTypeName { get; set; } = string.Empty;
	public string FileName { get; set; } = string.Empty;
	public string Language { get; set; } = "en";
	public string? Reason { get; set; }
	public DateTimeOffset GeneratedAt { get; set; }
	public Guid? ActorUserId { get; set; }
}

public class AdmissionCrmGeneratedDocumentAvailabilityDto
{
	public AdmissionCrmGeneratedDocumentType DocumentType { get; set; }
	public bool IsAvailable { get; set; }
	public string? ReasonCode { get; set; }
	public string Message { get; set; } = string.Empty;
}

public interface IAdmissionCrmGeneratedDocumentCommandService : IApplicationService
{
	Task<AdmissionCrmGeneratedDocumentDto> PreviewAsync(Guid leadId, PreviewAdmissionCrmGeneratedDocumentDto input);
	Task<AdmissionCrmGeneratedDocumentDto> IssueAsync(Guid leadId, IssueAdmissionCrmGeneratedDocumentDto input);
	Task<AdmissionCrmGeneratedDocumentDeliveryDto> SendEmailAsync(Guid leadId, SendAdmissionCrmGeneratedDocumentEmailDto input);
}

public interface IAdmissionCrmGeneratedDocumentQueryService : IApplicationService
{
	Task<IReadOnlyList<AdmissionCrmGeneratedDocumentAvailabilityDto>> GetAvailabilityAsync(Guid leadId);
	Task<IReadOnlyList<AdmissionCrmGeneratedDocumentHistoryDto>> GetHistoryAsync(Guid leadId);
}
