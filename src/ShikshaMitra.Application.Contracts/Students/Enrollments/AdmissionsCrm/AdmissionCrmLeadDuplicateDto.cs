using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadDuplicateDto
{
	public Guid LeadId { get; set; }
	public AdmissionInquiryStage Stage { get; set; }
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public string? PrimaryContactNo { get; set; }
	public string? Email { get; set; }
	public string MatchReason { get; set; } = string.Empty;
	public int ConfidenceScore { get; set; }
	public IReadOnlyList<string> MatchedFields { get; set; } = Array.Empty<string>();
	public DateTimeOffset LastActivityTime { get; set; }
}
