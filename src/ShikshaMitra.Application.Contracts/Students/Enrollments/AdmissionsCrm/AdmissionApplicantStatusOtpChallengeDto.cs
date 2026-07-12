using System;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionApplicantStatusOtpChallengeDto
{
	public string MaskedDestination { get; set; } = string.Empty;
	public DateTime OtpExpiresAt { get; set; }
	public bool DeliveryConfigured { get; set; }
	public string DeliveryStatus { get; set; } = string.Empty;
}
