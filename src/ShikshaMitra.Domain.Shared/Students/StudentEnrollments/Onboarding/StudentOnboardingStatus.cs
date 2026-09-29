namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public enum StudentOnboardingStatus
{
	Pending = 0,
	InProgress = 1,
	ReadyForActivation = 2,
	NeedsAttention = 3,
	Completed = 4,
	Cancelled = 5,
	Provisioning = 6
}

public enum StudentOnboardingStepStatus
{
	Pending = 0,
	InProgress = 1,
	Completed = 2,
	Blocked = 3,
	Waived = 4,
	NotRequired = 5
}

public enum StudentSeatReservationStatus
{
	Reserved = 0,
	Committed = 1,
	Released = 2,
	Expired = 3
}

public enum StudentIdentifierType
{
	GrNumber = 0,
	AdmissionNumber = 1,
	StudentCode = 2,
	TemporaryEnrollmentNumber = 3,
	SchoolDefined = 4
}

public enum StudentIdentifierAllocationStatus
{
	Reserved = 0,
	Committed = 1,
	Released = 2,
	Void = 3
}

public enum StudentOnboardingServiceHandoffStatus
{
	Pending = 0,
	Dispatched = 1,
	Acknowledged = 2,
	Failed = 3,
	Cancelled = 4
}
