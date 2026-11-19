namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public enum AdmissionOfferingAvailabilityStatus
{
	ConfigurationIncomplete = 0,
	NotOpenYet = 1,
	Open = 2,
	Closed = 3,
	Inactive = 4
}

public enum AdmissionOfferingConfigurationStatus
{
	Incomplete = 0,
	Ready = 1
}

public static class AdmissionOfferingValidationCodes
{
	public const string CycleMissing = "CycleMissing";
	public const string CycleUnavailable = "CycleUnavailable";
	public const string HierarchyInvalid = "HierarchyInvalid";
	public const string HierarchyOutOfScope = "HierarchyOutOfScope";
	public const string DateRangeInvalid = "DateRangeInvalid";
	public const string WorkflowMissing = "WorkflowMissing";
	public const string WorkflowNotPublished = "WorkflowNotPublished";
	public const string DirectWorkflowContainsInquiry = "DirectWorkflowContainsInquiry";
	public const string DirectWorkflowMustStartWithForm = "DirectWorkflowMustStartWithForm";
	public const string RteRequiresDirectRegistration = "RteRequiresDirectRegistration";
	public const string RteWorkflowContainsAssessment = "RteWorkflowContainsAssessment";
	public const string InquiryFormMissing = "InquiryFormMissing";
	public const string InquiryIntakeProfileMissing = "InquiryIntakeProfileMissing";
	public const string InquiryIntakeProfileInvalid = "InquiryIntakeProfileInvalid";
	public const string CounsellorAssignmentProfileMissing = "CounsellorAssignmentProfileMissing";
	public const string CounsellorAssignmentProfileInvalid = "CounsellorAssignmentProfileInvalid";
	public const string InquiryFormNotPublished = "InquiryFormNotPublished";
	public const string InquiryFormPurposeMismatch = "InquiryFormPurposeMismatch";
	public const string ApplicationFormMissing = "ApplicationFormMissing";
	public const string ApplicationFormNotPublished = "ApplicationFormNotPublished";
	public const string ApplicationFormPurposeMismatch = "ApplicationFormPurposeMismatch";
	public const string ApplicationFormIdentityIncomplete = "ApplicationFormIdentityIncomplete";
	public const string WorkflowFormMismatch = "WorkflowFormMismatch";
	public const string AssessmentProfileMissing = "AssessmentProfileMissing";
	public const string EligibilityFormMismatch = "EligibilityFormMismatch";
	public const string InquiryCounsellorUnavailable = "InquiryCounsellorUnavailable";
	public const string ReviewAssigneeUnavailable = "ReviewAssigneeUnavailable";
	public const string CampusMissing = "CampusMissing";
	public const string CampusInactive = "CampusInactive";
	public const string Duplicate = "Duplicate";
}
