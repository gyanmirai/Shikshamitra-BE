namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public enum AdmissionWorkflowGovernanceAction
{
	Created = 0,
	Validated = 1,
	Submitted = 2,
	Approved = 3,
	Rejected = 4,
	Scheduled = 5,
	Published = 6,
	Superseded = 7,
	Archived = 8,
	RollbackDraftCreated = 9
}

public enum AdmissionWorkflowNotificationType
{
	ApprovalRequested = 0,
	Approved = 1,
	Rejected = 2,
	Scheduled = 3,
	Published = 4,
	ScheduleFailed = 5,
	RollbackDraftCreated = 6
}
