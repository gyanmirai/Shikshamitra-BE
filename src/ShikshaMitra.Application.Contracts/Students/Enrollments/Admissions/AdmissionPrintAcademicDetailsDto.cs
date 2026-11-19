using System.Text.Json.Serialization;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed class AdmissionPrintAcademicDetailsDto
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Board { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Medium { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Class { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? AcademicYear { get; set; }
}
