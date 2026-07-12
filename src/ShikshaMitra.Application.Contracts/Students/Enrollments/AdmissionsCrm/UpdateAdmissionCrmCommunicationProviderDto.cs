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

	[MaxLength(AdmissionCrmConsts.MaxRoutingRulesJsonLength)]
	public string? SettingsJson { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxUrlLength)]
	public string? WebhookUrl { get; set; }

	public DateTime? LastValidatedAt { get; set; }
}
