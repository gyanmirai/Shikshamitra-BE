using System;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class PublicAdmissionInquirySubmitResultDto
{
	public Guid LeadId { get; set; }
	public string AcknowledgementMessage { get; set; } = string.Empty;
	public string StatusPath { get; set; } = string.Empty;
	public string StatusToken { get; set; } = string.Empty;
	public AdmissionInquiryStage Stage { get; set; }
}
