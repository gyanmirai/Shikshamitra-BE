using ShikshaMitra.Common;
using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentMedicalRecords(
	float? weight,
	float? height,
	float? bodyMassIndex,
	float? haemoglobin,
	string? allergies, 
	CovidVaccineDoses? covidVaccination,
	float? pulseRate,
	string? childImmunisation,
	string? immunisationRemarks
) : ValueObject
{
	public float? Weight { get; private set; } = weight;
	public float? Height { get; private set; } = height;
	public float? BodyMassIndex { get; private set; } = bodyMassIndex;
	public float? Haemoglobin { get; private set; } = haemoglobin;
	public string? Allergies { get; private set; } = allergies;
	public CovidVaccineDoses? CovidVaccination { get; private set; } = covidVaccination;
	public float? PulseRate { get; private set; } = haemoglobin;
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
		yield return PulseRate;
		yield return ChildImmunisation;
		yield return ImmunisationRemarks;
	}
}