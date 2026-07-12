using System;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Flags]
public enum AdmissionCrmExamReadinessField
{
	None = 0,
	Contact = 1,
	AdmissionIntent = 2,
	DesiredClass = 4,
	StudentName = 8,
	ParentGuardianName = 16,
	InquirySource = 32
}
