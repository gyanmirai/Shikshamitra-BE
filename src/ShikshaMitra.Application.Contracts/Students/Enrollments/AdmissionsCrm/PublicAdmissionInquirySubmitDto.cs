using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class PublicAdmissionInquirySubmitDto
{
	[Required]
	[MaxLength(AdmissionCrmConsts.MaxConsentVersionLength)]
	public required string ConsentVersion { get; set; }

	public bool ConsentAccepted { get; set; }

	[MinLength(1)]
	public IReadOnlyList<string> CommunicationPreferences { get; set; } = [];

	public Dictionary<string, JsonElement> FieldValues { get; set; } = new();
}
