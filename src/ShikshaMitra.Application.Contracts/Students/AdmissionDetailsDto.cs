using System;

namespace ShikshaMitra.Students;

public class AdmissionDetailsDto
{
	public DateTime? AdmissionDate { get; set; }
	public string AdmissionStandard { get; set; }
	public string AdmissionFormNumber { get; set; }
	public string AdmissionQuota { get; set; }
	public string PreviousSchool { get; set; }
	public DateTime? PreviousSchoolLeftDate { get; set; }
	public DateTime? PreviousLcDate { get; set; }
	public string PreviousLcNumber { get; set; }
}
