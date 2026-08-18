namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionErrorCodes
{
	public const string CycleCodeAlreadyExists = "Admissions.CycleCodeAlreadyExists";
	public const string CycleCannotBeModified = "Admissions.CycleCannotBeModified";
	public const string CycleDateRangeInvalid = "Admissions.CycleDateRangeInvalid";
	public const string OfferingDateRangeInvalid = "Admissions.OfferingDateRangeInvalid";
	public const string OfferingDuplicate = "Admissions.OfferingDuplicate";
	public const string OfferingCycleNotFound = "Admissions.OfferingCycleNotFound";
	public const string OfferingAcademicHierarchyInvalid = "Admissions.OfferingAcademicHierarchyInvalid";
	public const string InquiryDuplicate = "Admissions.InquiryDuplicate";
	public const string InquiryCycleNotFound = "Admissions.InquiryCycleNotFound";
	public const string InquiryDuplicateTargetInvalid = "Admissions.InquiryDuplicateTargetInvalid";
	public const string InquiryCounsellorRequired = "Admissions.InquiryCounsellorRequired";
	public const string InquiryCounsellorCandidatesRequired = "Admissions.InquiryCounsellorCandidatesRequired";
	public const string OfferingWorkflowNotPublished = "Admissions.OfferingWorkflowNotPublished";
	public const string OfferingFormNotPublished = "Admissions.OfferingFormNotPublished";
	public const string OfferingConfigurationIncomplete = "Admissions.Offering.ConfigurationIncomplete";
	public const string OfferingInactive = "Admissions.Offering.Inactive";
	public const string OfferingNotOpenYet = "Admissions.Offering.NotOpenYet";
	public const string OfferingClosed = "Admissions.Offering.Closed";
	public const string OfferingIdentityLocked = "Admissions.Offering.IdentityLocked";
	public const string OfferingHasApplications = "Admissions.Offering.HasApplications";
	public const string OfferingConcurrencyConflict = "Admissions.Offering.ConcurrencyConflict";
	public const string OfferingHierarchyOutOfScope = "Admissions.Offering.HierarchyOutOfScope";
	public const string OfferingCampusUnsupported = "Admissions.Offering.CampusUnsupported";
	public const string OfferingBulkValidationFailed = "Admissions.Offering.BulkValidationFailed";
	public const string OfferingStateCommandRequired = "Admissions.Offering.StateCommandRequired";
}
