using System;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// DTO for updating medical records of a student in Registration status
/// </summary>
public class UpdateMedicalRecordsDto
{
	/// <summary>
	/// Student's weight in kilograms
	/// </summary>
	public decimal? WeightKg { get; set; }

	/// <summary>
	/// Student's height in centimeters
	/// </summary>
	public decimal? HeightCm { get; set; }

	/// <summary>
	/// Body Mass Index
	/// </summary>
	public decimal? BodyMassIndex { get; set; }

	/// <summary>
	/// Pulse rate
	/// </summary>
	public int? PulseRate { get; set; }

	/// <summary>
	/// Hemoglobin level
	/// </summary>
	public decimal? Hemoglobin { get; set; }

	/// <summary>
	/// Known allergies
	/// </summary>
	public string? Allergies { get; set; }

	/// <summary>
	/// Child immunization details
	/// </summary>
	public string? ChildImmunization { get; set; }

	/// <summary>
	/// COVID vaccination doses received
	/// </summary>
	public int? CovidVaccinationDoses { get; set; }

	/// <summary>
	/// Additional immunization remarks
	/// </summary>
	public string? ImmunizationRemarks { get; set; }

	/// <summary>
	/// Blood group
	/// </summary>
	public string? BloodGroup { get; set; }

	/// <summary>
	/// Any chronic medical conditions
	/// </summary>
	public string? ChronicConditions { get; set; }

	/// <summary>
	/// Current medications
	/// </summary>
	public string? CurrentMedications { get; set; }
}
