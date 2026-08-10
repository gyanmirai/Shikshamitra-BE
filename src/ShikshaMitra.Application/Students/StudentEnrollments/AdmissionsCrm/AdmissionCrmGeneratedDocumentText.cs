using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public static class AdmissionCrmGeneratedDocumentText
{
	public static string NormalizeLanguage(string? language)
	{
		var normalized = (language ?? "en").Trim().ToLowerInvariant();
		return normalized is "en" or "gu" or "hi"
			? normalized
			: throw new BusinessException("AdmissionCrm:UnsupportedDocumentLanguage").WithData("Language", language);
	}

	public static string Slug(AdmissionCrmGeneratedDocumentType documentType) => documentType switch
	{
		AdmissionCrmGeneratedDocumentType.HallTicket => "hall-ticket",
		AdmissionCrmGeneratedDocumentType.RegistrationAcknowledgement => "registration-acknowledgement",
		AdmissionCrmGeneratedDocumentType.OfferLetter => "offer-letter",
		AdmissionCrmGeneratedDocumentType.AdmissionLetter => "admission-letter",
		AdmissionCrmGeneratedDocumentType.CorrectionNotice => "correction-notice",
		AdmissionCrmGeneratedDocumentType.CancellationLetter => "cancellation-letter",
		_ => "admission-document"
	};

	public static string TypeName(AdmissionCrmGeneratedDocumentType documentType, string language) =>
		language switch
		{
			"gu" => documentType switch
			{
				AdmissionCrmGeneratedDocumentType.HallTicket => "Exam Hall Ticket",
				AdmissionCrmGeneratedDocumentType.RegistrationAcknowledgement => "Registration Acknowledgement",
				AdmissionCrmGeneratedDocumentType.OfferLetter => "Seat Offer Letter",
				AdmissionCrmGeneratedDocumentType.AdmissionLetter => "Admission Letter",
				AdmissionCrmGeneratedDocumentType.CorrectionNotice => "Correction Notice",
				_ => "Cancellation Letter"
			},
			"hi" => documentType switch
			{
				AdmissionCrmGeneratedDocumentType.HallTicket => "Exam Hall Ticket",
				AdmissionCrmGeneratedDocumentType.RegistrationAcknowledgement => "Registration Acknowledgement",
				AdmissionCrmGeneratedDocumentType.OfferLetter => "Seat Offer Letter",
				AdmissionCrmGeneratedDocumentType.AdmissionLetter => "Admission Letter",
				AdmissionCrmGeneratedDocumentType.CorrectionNotice => "Correction Notice",
				_ => "Cancellation Letter"
			},
			_ => documentType switch
			{
				AdmissionCrmGeneratedDocumentType.HallTicket => "Entrance Exam Hall Ticket",
				AdmissionCrmGeneratedDocumentType.RegistrationAcknowledgement => "Registration Acknowledgement",
				AdmissionCrmGeneratedDocumentType.OfferLetter => "Seat Offer Letter",
				AdmissionCrmGeneratedDocumentType.AdmissionLetter => "Admission Letter",
				AdmissionCrmGeneratedDocumentType.CorrectionNotice => "Registration Correction Notice",
				_ => "Admission Cancellation Letter"
			}
		};

	public static string Label(string label, string language) => language switch
	{
		"gu" => label switch { "Applicant" => "Applicant", "Contact" => "Contact", "Desired class" => "Class", "Admission intent" => "Intent", "Generated" => "Generated", "Reference" => "Reference", "Reason" => "Reason", _ => label },
		"hi" => label switch { "Applicant" => "Applicant", "Contact" => "Contact", "Desired class" => "Class", "Admission intent" => "Intent", "Generated" => "Generated", "Reference" => "Reference", "Reason" => "Reason", _ => label },
		_ => label
	};

	public static string Instruction(AdmissionCrmGeneratedDocumentType documentType) => documentType switch
	{
		AdmissionCrmGeneratedDocumentType.HallTicket => "Please carry this hall ticket and the requested original identity documents on exam day.",
		AdmissionCrmGeneratedDocumentType.RegistrationAcknowledgement => "Keep this acknowledgement for future communication with the admissions office.",
		AdmissionCrmGeneratedDocumentType.OfferLetter => "Review the seat offer and respond before the configured expiry date.",
		AdmissionCrmGeneratedDocumentType.AdmissionLetter => "This letter confirms the admissions handoff. Complete any remaining school instructions before the first day.",
		AdmissionCrmGeneratedDocumentType.CorrectionNotice => "Please correct the listed registration information and resubmit through the secure applicant link.",
		_ => "This cancellation is recorded in the admissions timeline. Contact the admissions office for questions."
	};
}
