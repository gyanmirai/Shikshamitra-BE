using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

/// <summary>
/// Entity representing medical records of a student (1:1 relationship with Student aggregate)
/// Stores health and medical information
/// </summary>
public class StudentMedicalRecord : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	/// <summary>
	/// Student's weight in kilograms
	/// </summary>
	public decimal? WeightKg { get; private set; }

	/// <summary>
	/// Student's height in centimeters
	/// </summary>
	public decimal? HeightCm { get; private set; }

	/// <summary>
	/// Body Mass Index
	/// </summary>
	public decimal? BodyMassIndex { get; private set; }

	/// <summary>
	/// Pulse rate
	/// </summary>
	public int? PulseRate { get; private set; }

	/// <summary>
	/// Hemoglobin level
	/// </summary>
	public decimal? Hemoglobin { get; private set; }

	/// <summary>
	/// Known allergies
	/// </summary>
	public string? Allergies { get; private set; }

	/// <summary>
	/// Child immunization details
	/// </summary>
	public string? ChildImmunization { get; private set; }

	/// <summary>
	/// COVID vaccination doses received
	/// </summary>
	public int? CovidVaccinationDoses { get; private set; }

	/// <summary>
	/// Additional immunization remarks
	/// </summary>
	public string? ImmunizationRemarks { get; private set; }

	/// <summary>
	/// Blood group
	/// </summary>
	public string? BloodGroup { get; private set; }

	/// <summary>
	/// Any chronic medical conditions
	/// </summary>
	public string? ChronicConditions { get; private set; }

	/// <summary>
	/// Current medications
	/// </summary>
	public string? CurrentMedications { get; private set; }

	private StudentMedicalRecord()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public StudentMedicalRecord(
		Guid id,
		Guid? tenantId)
		: base(id)
	{
		TenantId = tenantId;
	}

	/// <summary>
	/// Updates all medical record information at once
	/// </summary>
	public void UpdateMedicalRecords(
		decimal? weightKg,
		decimal? heightCm,
		decimal? bodyMassIndex,
		int? pulseRate,
		decimal? hemoglobin,
		string? allergies,
		string? childImmunization,
		int? covidVaccinationDoses,
		string? immunizationRemarks,
		string? bloodGroup,
		string? chronicConditions,
		string? currentMedications)
	{
		WeightKg = weightKg;
		HeightCm = heightCm;
		BodyMassIndex = bodyMassIndex;
		PulseRate = pulseRate;
		Hemoglobin = hemoglobin;
		Allergies = allergies;
		ChildImmunization = childImmunization;
		CovidVaccinationDoses = covidVaccinationDoses;
		ImmunizationRemarks = immunizationRemarks;
		BloodGroup = bloodGroup;
		ChronicConditions = chronicConditions;
		CurrentMedications = currentMedications;
	}
}
