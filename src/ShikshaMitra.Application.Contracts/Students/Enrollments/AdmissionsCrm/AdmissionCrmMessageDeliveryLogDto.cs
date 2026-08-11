using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmMessageDeliveryLogDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid? LeadId { get; set; }
	public Guid? TemplateId { get; set; }
	public AdmissionCrmMessageChannel Channel { get; set; }
	public string Recipient { get; set; } = string.Empty;
	public string NormalizedRecipient { get; set; } = string.Empty;
	public string? ProviderName { get; set; }
	public string? ProviderMessageId { get; set; }
	public string? SubjectSnapshot { get; set; }
	public string BodySnapshot { get; set; } = string.Empty;
	public AdmissionCrmGeneratedDocumentType? GeneratedDocumentType { get; set; }
	public string? AttachmentLanguage { get; set; }
	public string? AttachmentFileName { get; set; }
	public string? AttachmentContentType { get; set; }
	public string Status { get; set; } = string.Empty;
	public string StatusDetails { get; set; } = string.Empty;
	public int AttemptCount { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? LastAttemptAt { get; set; }
	public DateTime? SentAt { get; set; }
	public DateTime? DeliveredAt { get; set; }
}
