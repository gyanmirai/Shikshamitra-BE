namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public enum AdmissionApplicationStatus
{
	Draft = 0,
	Submitted = 1,
	Withdrawn = 2,
	Rejected = 3
}

public enum AdmissionRuntimeStageStatus
{
	Pending = 0,
	Active = 1,
	Completed = 2,
	Skipped = 3,
	Failed = 4,
	Cancelled = 5
}

public enum AdmissionWorkflowInstanceStatus
{
	Active = 0,
	Completed = 1,
	Cancelled = 2,
	Failed = 3,
	NotStarted = 4
}

public enum AdmissionWorkflowTransitionAction
{
	Completed = 0,
	Skipped = 1,
	AdminOverride = 2
}

public enum AdmissionFormInstanceStatus
{
	Draft = 0,
	Submitted = 1,
	Reopened = 2
}
