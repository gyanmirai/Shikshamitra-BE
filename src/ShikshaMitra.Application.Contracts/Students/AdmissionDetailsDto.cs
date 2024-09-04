using System;

namespace ShikshaMitra.Students;

public class AdmissionDetailsDto
{
	public DateOnly? AdmissionDate { get; set; }
	public string? AdmissionStandard { get; set; }
	public string? AdmissionFormNumber { get; set; }
	public string? AdmissionQuota { get; set; }
	public string? PreviousSchool { get; set; }
	public DateOnly? PreviousSchoolLeftDate { get; set; }
	public DateOnly? PreviousLcDate { get; set; }
	public string? PreviousLcNumber { get; set; }
}
