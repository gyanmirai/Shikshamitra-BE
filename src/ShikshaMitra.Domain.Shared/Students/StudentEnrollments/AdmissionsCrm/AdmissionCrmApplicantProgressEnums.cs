namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public enum AdmissionCrmApplicantStepKey
{
	Inquiry = 0,
	Exam = 1,
	Registration = 2,
	Finance = 3,
	Placement = 4,
	Activation = 5
}

public enum AdmissionCrmApplicantStepStatus
{
	NotStarted = 0,
	InProgress = 1,
	Complete = 2,
	Skipped = 3,
	Blocked = 4,
	NeedsReview = 5
}

public enum AdmissionCrmPipelineColumnKey
{
	NewInquiry = 0,
	FollowUp = 1,
	ExamPending = 2,
	ExamDone = 3,
	Eligible = 4,
	RegistrationPending = 5,
	RegistrationDone = 6,
	FinanceVerification = 7,
	PlacementPending = 8,
	ReadyForActivation = 9,
	Activated = 10
}

public enum AdmissionCrmClosedOutcome
{
	Rejected = 0,
	Withdrawn = 1,
	Ineligible = 2,
	Cancelled = 3,
	Duplicate = 4,
	NotInterested = 5
}
