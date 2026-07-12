using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmCommunicationProviderDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public AdmissionCrmMessageChannel Channel { get; set; }
	public string Status { get; set; } = string.Empty;
	public bool IsEnabled { get; set; }
	public string? ProviderName { get; set; }
	public string? SenderId { get; set; }
	public string? SettingsJson { get; set; }
	public string? WebhookUrl { get; set; }
	public DateTime? LastValidatedAt { get; set; }
}
