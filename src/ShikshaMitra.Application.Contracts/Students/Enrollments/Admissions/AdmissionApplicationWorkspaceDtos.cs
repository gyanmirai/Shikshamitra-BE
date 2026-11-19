using System;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionApplicationDto
{
	/// <summary>Authoritative on list/detail reads. Null on lightweight mutation responses; once true, corrections remain in Applications.</summary>
	public bool? HasReachedDecision { get; set; }
	public DateTime? DecisionEnteredAt { get; set; }
	public string? StandardName { get; set; }
	public string? ContactPhone { get; set; }
	public string? ContactEmail { get; set; }
}
