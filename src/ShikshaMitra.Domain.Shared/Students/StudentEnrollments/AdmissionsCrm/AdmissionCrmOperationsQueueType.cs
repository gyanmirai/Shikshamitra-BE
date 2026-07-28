namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public enum AdmissionCrmOperationsQueueType
{
	UnassignedExam = 0,
	CapacityConflict = 1,
	NoShow = 2,
	ExpiringRegistrationLink = 3,
	IncompleteRegistration = 4,
	RejectedDocument = 5,
	DocumentVerificationBacklog = 6,
	AdmissionReady = 7,
	PolicyException = 8,
	PaymentCondition = 9,
	HouseholdResolution = 10,
	IdentityConflict = 11,
	FinalApproval = 12,
	ActivationRepair = 13,
	RouteEligibilityReview = 14,
	GovernmentAllotmentReconciliation = 15,
	TransferEvidencePending = 16,
	ReturningIdentityMatch = 17,
	CancellationReview = 18,
	RouteAppealReview = 19,
	ServiceHandoffException = 20,
	RegistrationInvitationPending = 21,
	PendingPaperEntry = 22
}
