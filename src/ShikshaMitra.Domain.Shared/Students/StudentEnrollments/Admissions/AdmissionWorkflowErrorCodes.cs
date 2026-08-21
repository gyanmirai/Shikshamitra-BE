namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionWorkflowErrorCodes
{
	public const string PublishedImmutable = "Admissions.WorkflowPublishedImmutable";
	public const string DraftRequired = "Admissions.WorkflowDraftRequired";
	public const string CodeAlreadyExists = "Admissions.WorkflowCodeAlreadyExists";
	public const string NoStages = "Admissions.WorkflowNoStages";
	public const string DuplicateStageCode = "Admissions.WorkflowDuplicateStageCode";
	public const string InvalidInitialStage = "Admissions.WorkflowInvalidInitialStage";
	public const string InvalidFinalStage = "Admissions.WorkflowInvalidFinalStage";
	public const string InvalidTransition = "Admissions.WorkflowInvalidTransition";
	public const string DuplicateTransition = "Admissions.WorkflowDuplicateTransition";
	public const string DuplicateDefaultTransition = "Admissions.WorkflowDuplicateDefaultTransition";
	public const string InvalidCondition = "Admissions.WorkflowInvalidCondition";
	public const string RuntimeHandlerUnavailable = "Admissions.WorkflowRuntimeHandlerUnavailable";
	public const string OrphanStage = "Admissions.WorkflowOrphanStage";
	public const string DeadEndStage = "Admissions.WorkflowDeadEndStage";
	public const string UnreachableStage = "Admissions.WorkflowUnreachableStage";
	public const string TerminalUnreachable = "Admissions.WorkflowTerminalUnreachable";
	public const string CycleDetected = "Admissions.WorkflowCycleDetected";
	public const string WorkflowInUse = "Admissions.WorkflowInUse";
	public const string ConcurrencyConflict = "Admissions.WorkflowConcurrencyConflict";
}
