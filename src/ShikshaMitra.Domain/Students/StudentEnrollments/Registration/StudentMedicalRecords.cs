using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentMedicalRecords(
	float? weight,
	float? height,
	int? bodyMassIndex,
	string? haemoglobin,
	string? allergies, 
	string? covidVaccination,
	string? childImmunisation,
	string? immunisationRemarks
) : ValueObject
{
	public float? Weight { get; private set; } = weight;
	public float? Height { get; private set; } = height;
	public int? BodyMassIndex { get; private set; } = bodyMassIndex;
	public string? Haemoglobin { get; private set; } = haemoglobin;
	public string? Allergies { get; private set; } = allergies;
	public string? CovidVaccination { get; private set; } = covidVaccination;
	public string? ChildImmunisation { get; private set; } = childImmunisation;
	public string? ImmunisationRemarks { get; private set; } = immunisationRemarks;
	
	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return Weight;
		yield return Height;
		yield return BodyMassIndex;
		yield return Haemoglobin;
		yield return Allergies;
		yield return CovidVaccination;
		yield return ChildImmunisation;
		yield return ImmunisationRemarks;
	}
}