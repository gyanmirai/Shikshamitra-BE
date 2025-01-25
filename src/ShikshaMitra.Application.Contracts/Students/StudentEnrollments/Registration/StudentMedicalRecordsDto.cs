using ShikshaMitra.Common;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentMedicalRecordsDto
{
	public float? Weight { get; set; }
	public float? Height { get; set; }
	public float? BodyMassIndex { get; set; }
	public float? Haemoglobin { get; set; }
	public string? Allergies { get; set; }
	public CovidVaccineDoses? CovidVaccination { get; set; }
	public float? PulseRate { get;  set; }
	public string? ChildImmunisation { get; set; }
	public string? ImmunisationRemarks { get; set; }

}