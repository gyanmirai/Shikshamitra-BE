using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class PublicAdmissionPortalDto
{
	public string SchoolName { get; set; } = "School";
	public string? LogoDataUrl { get; set; }
	public string? AdmissionsPhone { get; set; }
	public string? AdmissionsEmail { get; set; }
	public string? OfficeHours { get; set; }
	public string? HelpText { get; set; }
	public string? PrimaryColor { get; set; }
	public string DefaultLanguage { get; set; } = "en";
	public IReadOnlyList<string> SupportedLanguages { get; set; } = ["en", "gu", "hi"];
}
