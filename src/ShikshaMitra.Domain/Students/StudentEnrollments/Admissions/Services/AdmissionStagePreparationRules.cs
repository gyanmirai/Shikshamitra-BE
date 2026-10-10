namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Preparation never enters a stage, selects a route, or changes completed evidence.</summary>
public static class AdmissionStagePreparationRules
{
	public static bool CanPrepare(AdmissionApplication application, AdmissionWorkflowInstanceStatus workflowStatus,
		AdmissionRuntimeStageStatus stageStatus) => application.CanEndJourney &&
		workflowStatus == AdmissionWorkflowInstanceStatus.Active &&
		stageStatus is AdmissionRuntimeStageStatus.Pending or AdmissionRuntimeStageStatus.Active;
}
