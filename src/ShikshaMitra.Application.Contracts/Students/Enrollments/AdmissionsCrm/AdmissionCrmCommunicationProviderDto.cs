using System;
using System.Collections.Generic;
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
	public AdmissionCrmCommunicationProviderSettingsDto Settings { get; set; } = new();
	public string? WebhookUrl { get; set; }
	public DateTime? LastValidatedAt { get; set; }
}

public class AdmissionCrmCommunicationProviderSettingsDto
{
	public string Adapter { get; set; } = "manual";
	public string PayloadMode { get; set; } = "default";
	public string? ResponseProviderMessageIdPath { get; set; }
	public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public string? CallbackSecret { get; set; }
	public bool HasStoredHeaderValues { get; set; }
	public bool HasStoredParameterValues { get; set; }
	public bool HasCallbackSecret { get; set; }
	public AdmissionCrmCommunicationCallbackMappingsDto CallbackMappings { get; set; } = new();
}

public class AdmissionCrmCommunicationCallbackMappingsDto
{
	public string? StatusPath { get; set; }
	public string? StatusDetailsPath { get; set; }
	public string? ProviderMessageIdPath { get; set; }
}
