namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public enum AdmissionCrmJourneyTrigger
{
	LeadCreated = 0,
	StageReached = 1,
	SlaMissed = 2,
	StaleMarked = 3,
	ExamScheduled = 4,
	ExamCompleted = 5,
	EligibilityDecided = 6,
	RegistrationIssued = 7,
	RegistrationIncomplete = 8,
	RegistrationExpiring = 9,
	RegistrationSubmitted = 10,
	RegistrationCorrectionRequested = 11,
	RegistrationVerified = 12,
	DocumentUploaded = 13,
	DocumentRejected = 14,
	DocumentPending = 15,
	ExamNoShow = 16,
	AdmissionReady = 17
}
