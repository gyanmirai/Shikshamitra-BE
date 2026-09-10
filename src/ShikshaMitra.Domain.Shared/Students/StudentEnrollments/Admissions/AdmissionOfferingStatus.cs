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
	public const string InquiryFormMissing = "InquiryFormMissing";
	public const string InquiryFormNotPublished = "InquiryFormNotPublished";
	public const string ApplicationFormMissing = "ApplicationFormMissing";
	public const string ApplicationFormNotPublished = "ApplicationFormNotPublished";
	public const string ApplicationFormIdentityIncomplete = "ApplicationFormIdentityIncomplete";
	public const string CampusMissing = "CampusMissing";
	public const string CampusInactive = "CampusInactive";
	public const string Duplicate = "Duplicate";
}
