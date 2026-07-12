namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public enum AdmissionCrmDataQualityIssueType
{
	DuplicateApplicant = 0,
	DuplicatePerson = 1,
	AcceptedOfferWithoutReservation = 2,
	ActivatedStudentWithoutLineage = 3,
	DuplicateIdentifier = 4,
	InvalidPlacement = 5,
	ImpossibleActivationState = 6,
	StalePaymentState = 7,
	OrphanedSeatReservation = 8,
	MissingAuditReference = 9,
	ServiceHandoffFailure = 10,
	StalePrivacyRequest = 11,
	ExpiredSensitiveExportContent = 12
}
