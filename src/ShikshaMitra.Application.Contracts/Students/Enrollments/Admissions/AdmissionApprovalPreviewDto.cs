namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionApplicationStageDto
{
	public bool IsTerminalStage { get; set; }
}

public partial class AdmissionFinalizationPreviewDto
{
	public AdmissionFinalizationPrimaryContactDto? PrimaryContact { get; set; }
	public string? JoiningDate { get; set; }
}

public class AdmissionFinalizationPrimaryContactDto
{
	public string Name { get; set; } = string.Empty;
	public string? Mobile { get; set; }
	public string? Email { get; set; }
	public string? Relationship { get; set; }
}
