using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class UpdateAdmissionCrmCommunicationProviderDto
{
	public bool IsEnabled { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? ProviderName { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? SenderId { get; set; }


	public AdmissionCrmCommunicationProviderSettingsDto Settings { get; set; } = new();

	[MaxLength(AdmissionCrmConsts.MaxUrlLength)]
	public string? WebhookUrl { get; set; }

	public DateTime? LastValidatedAt { get; set; }
}
