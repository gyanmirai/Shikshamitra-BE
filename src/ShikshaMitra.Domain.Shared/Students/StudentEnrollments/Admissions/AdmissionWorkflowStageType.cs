namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public enum AdmissionWorkflowStageType
{
	Inquiry = 0,
	Form = 1,
	Assessment = 2,
	Eligibility = 3,
	Payment = 4,
	DocumentVerification = 5,
	Review = 6,
	Approval = 7,
	Offer = 8,
	Seat = 9,
	Enrollment = 10,
	Custom = 11,
	// Appended to preserve the persisted numeric values of existing stage types.
	Appointment = 12
}
