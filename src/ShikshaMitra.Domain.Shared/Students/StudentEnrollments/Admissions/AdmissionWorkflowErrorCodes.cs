namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionWorkflowErrorCodes
{
	public const string PublishedImmutable = "Admissions.WorkflowPublishedImmutable";
	public const string CodeAlreadyExists = "Admissions.WorkflowCodeAlreadyExists";
	public const string NoStages = "Admissions.WorkflowNoStages";
	public const string DuplicateStageCode = "Admissions.WorkflowDuplicateStageCode";
	public const string InvalidInitialStage = "Admissions.WorkflowInvalidInitialStage";
	public const string InvalidFinalStage = "Admissions.WorkflowInvalidFinalStage";
	public const string InvalidTransition = "Admissions.WorkflowInvalidTransition";
	public const string OrphanStage = "Admissions.WorkflowOrphanStage";
	public const string CycleDetected = "Admissions.WorkflowCycleDetected";
}
