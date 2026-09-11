namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionApplicationDto
{
	public AdmissionReviewDecisionCapabilitiesDto? ReviewDecision { get; set; }
}
public partial class AdmissionApplicationStageDto
{
	public bool ReviewEnabled { get; set; }
}
