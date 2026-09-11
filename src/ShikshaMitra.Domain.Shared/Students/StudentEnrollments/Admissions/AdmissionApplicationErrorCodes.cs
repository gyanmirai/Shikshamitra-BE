namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionApplicationErrorCodes
{
	public const string OfferingNotAvailable = "Admissions:ApplicationOfferingNotAvailable";
	public const string ConfigurationNotPublished = "Admissions:ApplicationConfigurationNotPublished";
	public const string AlreadySubmitted = "Admissions:ApplicationAlreadySubmitted";
	public const string ValidationFailed = "Admissions:ApplicationValidationFailed";
	public const string ConcurrencyConflict = "Admissions:ApplicationConcurrencyConflict";
	public const string WorkflowAlreadyCompleted = "Admissions:ApplicationWorkflowAlreadyCompleted";
	public const string InvalidCurrentStage = "Admissions:ApplicationInvalidCurrentStage";
	public const string NoEligibleTransition = "Admissions:ApplicationNoEligibleTransition";
	public const string AmbiguousTransition = "Admissions:ApplicationAmbiguousTransition";
	public const string ManualSkipNotAllowed = "Admissions:ApplicationManualSkipNotAllowed";
	public const string IdempotencyKeyRequired = "Admissions:ApplicationIdempotencyKeyRequired";
	public const string StageCannotAdvance = "Admissions:ApplicationStageCannotAdvance";
	public const string FormNotSubmitted = "Admissions:ApplicationFormNotSubmitted";
	public const string UnknownFormField = "Admissions:ApplicationUnknownFormField";
}
