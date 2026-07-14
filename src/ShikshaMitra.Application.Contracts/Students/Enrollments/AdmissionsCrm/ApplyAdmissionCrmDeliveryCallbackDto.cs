using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class ApplyAdmissionCrmDeliveryCallbackDto
{
	public string Status { get; set; } = string.Empty;

	public string? StatusDetails { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxProviderMessageIdLength)]
	public string? ProviderMessageId { get; set; }

	public string CallbackSecret { get; set; } = string.Empty;

	public Dictionary<string, JsonElement> Payload { get; set; } = new();
}
