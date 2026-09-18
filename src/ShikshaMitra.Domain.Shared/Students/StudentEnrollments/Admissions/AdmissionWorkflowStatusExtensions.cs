namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionWorkflowStatusExtensions
{
	public static bool IsRuntimeVersion(this AdmissionWorkflowStatus status) =>
		status is AdmissionWorkflowStatus.Published or AdmissionWorkflowStatus.Superseded or AdmissionWorkflowStatus.Archived;

	public static bool CanConfigureOffering(this AdmissionWorkflowStatus status) =>
		status is AdmissionWorkflowStatus.Published or AdmissionWorkflowStatus.Superseded;
}
