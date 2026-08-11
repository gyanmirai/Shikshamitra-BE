using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class ApplyAdmissionCrmDeliveryCallbackDto
{
	public string Status { get; set; } = string.Empty;

	public string? StatusDetails { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxProviderMessageIdLength)]
	public string? ProviderMessageId { get; set; }

	[JsonIgnore]
	public string CallbackSignature { get; set; } = string.Empty;

	[JsonIgnore]
	public string CallbackTimestamp { get; set; } = string.Empty;

	[JsonIgnore]
	public string CallbackNonce { get; set; } = string.Empty;

	[JsonIgnore]
	public string RawBody { get; set; } = string.Empty;

	public Dictionary<string, JsonElement> Payload { get; set; } = new();
}
