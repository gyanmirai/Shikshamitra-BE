namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFormPrintSettingsDto
{
	public string? Template { get; set; }
	public string Layout { get; set; } = "compact";
	public string PaperSize { get; set; } = "A4";
	public bool ShowSchoolHeader { get; set; } = true;
	public bool ShowPhoto { get; set; } = true;
	public bool ShowSignature { get; set; } = true;
}
