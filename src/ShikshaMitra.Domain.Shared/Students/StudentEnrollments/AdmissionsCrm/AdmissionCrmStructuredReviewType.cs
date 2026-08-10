namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public enum AdmissionCrmStructuredReviewStatus
{
	InReview = 0,
	ReadyForDecision = 1,
	Closed = 2,
	Cancelled = 3
}

public enum AdmissionCrmReviewAssignmentStatus
{
	Assigned = 0,
	Submitted = 1,
	Recused = 2,
	Cancelled = 3
}

public enum AdmissionCrmReviewRecommendation
{
	Admit = 0,
	Waitlist = 1,
	Reject = 2,
	MoreInformation = 3
}
