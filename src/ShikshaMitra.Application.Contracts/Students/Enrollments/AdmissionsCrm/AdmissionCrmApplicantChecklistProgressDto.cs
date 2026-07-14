using System;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmApplicantChecklistProgressDto
{
	public Guid? Id { get; set; }
	public Guid LeadId { get; set; }
	public Guid AcademicYearId { get; set; }
	public string Key { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public string? Details { get; set; }
	public bool IsRequired { get; set; }
	public AdmissionCrmApplicantChecklistProgressStatus Status { get; set; }
	public string StatusLabel { get; set; } = string.Empty;
	public bool IsComplete { get; set; }
	public string? Notes { get; set; }
	public DateTimeOffset? StatusChangedAt { get; set; }
}
