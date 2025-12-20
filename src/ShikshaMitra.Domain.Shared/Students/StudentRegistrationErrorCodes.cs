namespace ShikshaMitra.Students;

/// <summary>
/// Localization keys for student registration error messages.
/// All keys map to entries in the ShikshaMitra localization resource (en.json).
/// </summary>
public static class StudentRegistrationErrorCodes
{
	/// <summary>
	/// Error code: ShikshaMitra.Student:AcademicMediumNotFound
	/// Message: Academic Medium not found with ID: {academicMediumId}
	/// </summary>
	public const string AcademicMediumNotFound = "ShikshaMitra.Student:AcademicMediumNotFound";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidStudentStatus
	/// Message: Student must be in {expectedStatus} status. Current status: {currentStatus}
	/// </summary>
	public const string InvalidStudentStatus = "ShikshaMitra.Student:InvalidStudentStatus";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidStudentStatusForGeneralInfo
	/// Message: Student must be in RegistrationFormIssued or Registration status to update general information. Current status: {currentStatus}
	/// </summary>
	public const string InvalidStudentStatusForGeneralInfo = "ShikshaMitra.Student:InvalidStudentStatusForGeneralInfo";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidStudentStatusForFamilyInfo
	/// Message: Student must be in Registration or Registration Form Issued status to view family information. Current status: {currentStatus}
	/// </summary>
	public const string InvalidStudentStatusForFamilyInfo = "ShikshaMitra.Student:InvalidStudentStatusForFamilyInfo";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidStudentStatusForFamilyUpdate
	/// Message: Student must be in Registration or Registration Form Issued status to update family information. Current status: {currentStatus}
	/// </summary>
	public const string InvalidStudentStatusForFamilyUpdate = "ShikshaMitra.Student:InvalidStudentStatusForFamilyUpdate";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidStudentStatusForCommunicationInfo
	/// Message: Student must be in Registration or Registration Form Issued status to view communication information. Current status: {currentStatus}
	/// </summary>
	public const string InvalidStudentStatusForCommunicationInfo = "ShikshaMitra.Student:InvalidStudentStatusForCommunicationInfo";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidStudentStatusForCommunicationUpdate
	/// Message: Student must be in Registration or Registration Form Issued status to update communication information. Current status: {currentStatus}
	/// </summary>
	public const string InvalidStudentStatusForCommunicationUpdate = "ShikshaMitra.Student:InvalidStudentStatusForCommunicationUpdate";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidStudentStatusForAdditionalDetails
	/// Message: Student must be in Registration or Registration Form Issued status to view additional details. Current status: {currentStatus}
	/// </summary>
	public const string InvalidStudentStatusForAdditionalDetails = "ShikshaMitra.Student:InvalidStudentStatusForAdditionalDetails";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidStudentStatusForAdditionalDetailsUpdate
	/// Message: Student must be in Registration or Registration Form Issued status to update additional details. Current status: {currentStatus}
	/// </summary>
	public const string InvalidStudentStatusForAdditionalDetailsUpdate = "ShikshaMitra.Student:InvalidStudentStatusForAdditionalDetailsUpdate";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidStudentStatusForMedicalRecords
	/// Message: Student must be in Registration or Registration Form Issued status to view medical records. Current status: {currentStatus}
	/// </summary>
	public const string InvalidStudentStatusForMedicalRecords = "ShikshaMitra.Student:InvalidStudentStatusForMedicalRecords";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidStudentStatusForMedicalRecordsUpdate
	/// Message: Student must be in Registration or Registration Form Issued status to update medical records. Current status: {currentStatus}
	/// </summary>
	public const string InvalidStudentStatusForMedicalRecordsUpdate = "ShikshaMitra.Student:InvalidStudentStatusForMedicalRecordsUpdate";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidStudentStatusForPreviousSchool
	/// Message: Student must be in Registration or Registration Form Issued status to view previous school details. Current status: {currentStatus}
	/// </summary>
	public const string InvalidStudentStatusForPreviousSchool = "ShikshaMitra.Student:InvalidStudentStatusForPreviousSchool";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidStudentStatusForPreviousSchoolUpdate
	/// Message: Student must be in Registration or Registration Form Issued status to update previous school details. Current status: {currentStatus}
	/// </summary>
	public const string InvalidStudentStatusForPreviousSchoolUpdate = "ShikshaMitra.Student:InvalidStudentStatusForPreviousSchoolUpdate";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidStudentStatusForBankDetails
	/// Message: Student must be in Registration or Registration Form Issued status to view bank details. Current status: {currentStatus}
	/// </summary>
	public const string InvalidStudentStatusForBankDetails = "ShikshaMitra.Student:InvalidStudentStatusForBankDetails";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidStudentStatusForBankDetailsUpdate
	/// Message: Student must be in Registration or Registration Form Issued status to update bank details. Current status: {currentStatus}
	/// </summary>
	public const string InvalidStudentStatusForBankDetailsUpdate = "ShikshaMitra.Student:InvalidStudentStatusForBankDetailsUpdate";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidSiblingStudent
	/// Message: Sibling student with ID {siblingId} not found
	/// </summary>
	public const string InvalidSiblingStudent = "ShikshaMitra.Student:InvalidSiblingStudent";

	/// <summary>
	/// Error code: ShikshaMitra.Student:MissingAdditionalDetail
	/// Message: Student additional detail not found. Please complete the general information step first.
	/// </summary>
	public const string MissingAdditionalDetail = "ShikshaMitra.Student:MissingAdditionalDetail";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidStatusForRegistrationView
	/// Message: Student must be in Registration or RegistrationFormIssued status. Current status: {currentStatus}
	/// </summary>
	public const string InvalidStatusForRegistrationView = "ShikshaMitra.Student:InvalidStatusForRegistrationView";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidStatusForRegistrationFormView
	/// Message: Student must be in RegistrationFormIssued status to view registration form. Current status: {currentStatus}
	/// </summary>
	public const string InvalidStatusForRegistrationFormView = "ShikshaMitra.Student:InvalidStatusForRegistrationFormView";

	/// <summary>
	/// Error code: ShikshaMitra.Student:InvalidStatusForRegistrationListView
	/// Message: Student must be in Registration status to view registration. Current status: {currentStatus}
	/// </summary>
	public const string InvalidStatusForRegistrationListView = "ShikshaMitra.Student:InvalidStatusForRegistrationListView";
}
