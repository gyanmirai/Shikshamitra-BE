namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmStaleArchiveRunResultDto
{
	public int MarkedStaleCount { get; set; }
	public int ArchivedCount { get; set; }
	public int CreatedOwnerNotificationTaskCount { get; set; }
	public int SkippedExistingOwnerNotificationTaskCount { get; set; }
}
