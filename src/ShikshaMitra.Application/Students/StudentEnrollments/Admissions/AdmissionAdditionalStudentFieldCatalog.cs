using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>
/// Additional admission answers that follow the applicant into the Student record.
/// The same binding key is used for inquiry, registration, and onboarding handoff.
/// </summary>
internal static class AdmissionAdditionalStudentFieldCatalog
{
	private const string PropertyPrefix = "AdmissionStudentField.";
	private static readonly IReadOnlyList<AdmissionFormSystemFieldMetadataDto> Fields =
	[
		// Student profile
		Field("Applicant.ProfilePhoto", "Student profile photo"),
		Field("Identity.AadhaarNumber", "Student Aadhaar number"),
		Field("Identity.PanNumber", "Student PAN number"),
		Field("Family.IsSingleParentChild", "Single-parent child", AdmissionFormFieldType.Checkbox),
		Field("Family.IsSingleChild", "Single child", AdmissionFormFieldType.Checkbox),
		Field("Family.SiblingStudentIds", "Sibling students", AdmissionFormFieldType.MultiSelect, "{\"source\":\"StudentSiblings\"}"),
		Field("Applicant.PlaceOfBirth", "Place of birth"),
		Field("Applicant.Nationality", "Nationality"),
		Field("Applicant.MotherTongue", "Mother tongue"),
		Field("Applicant.Religion", "Religion"),
		Field("Applicant.SocialCategory", "Social category"),
		Field("Applicant.Caste", "Caste"),
		Field("Applicant.HasDisability", "Has a disability", AdmissionFormFieldType.Checkbox),
		Field("Applicant.DisabilityDetails", "Disability details", AdmissionFormFieldType.Textarea),
		// Parents and guardians
		Field("Guardian.Relationship", "Guardian relationship"),
		Field("Father.Name", "Father's name"),
		Field("Father.Mobile", "Father's mobile", AdmissionFormFieldType.Phone),
		Field("Father.Occupation", "Father's occupation"),
		Field("Mother.Name", "Mother's name"),
		Field("Mother.Mobile", "Mother's mobile", AdmissionFormFieldType.Phone),
		Field("Mother.Occupation", "Mother's occupation"),
		Field("Guardian.AlternateName", "Alternate guardian's name"),
		// Contact and address
		Field("Applicant.Mobile", "Student mobile", AdmissionFormFieldType.Phone),
		Field("Applicant.Email", "Student email", AdmissionFormFieldType.Email),
		Field("Contact.SecondaryPhone", "Secondary contact number", AdmissionFormFieldType.Phone),
		Field("Address.AddressLine3", "Address line 3"),
		Field("Emergency.ContactName", "Emergency contact name"),
		Field("Emergency.ContactPhone", "Emergency contact number", AdmissionFormFieldType.Phone),
		// Previous school
		Field("PreviousSchool.Name", "Previous school name"),
		Field("PreviousSchool.Board", "Previous school board"),
		Field("PreviousSchool.Medium", "Previous school medium"),
		Field("PreviousSchool.LastClass", "Last class attended"),
		Field("PreviousSchool.AcademicYear", "Last academic year"),
		Field("PreviousSchool.TCNumber", "Transfer certificate number"),
		Field("PreviousSchool.DateOfLeaving", "Date of leaving", AdmissionFormFieldType.Date),
		// Health and support
		Field("Medical.BloodGroup", "Blood group", AdmissionFormFieldType.Select,
			"[\"A+\",\"A-\",\"B+\",\"B-\",\"AB+\",\"AB-\",\"O+\",\"O-\"]"),
		Field("Medical.Allergies", "Allergies", AdmissionFormFieldType.Textarea),
		Field("Medical.MedicalConditions", "Medical conditions", AdmissionFormFieldType.Textarea),
		Field("Medical.CurrentMedication", "Current medication", AdmissionFormFieldType.Textarea),
		Field("Medical.AccessibilitySupport", "Accessibility support needed", AdmissionFormFieldType.Textarea),
		// RTE and eligibility
		Field("Eligibility.RteCategory", "RTE eligibility category"),
		Field("Eligibility.IsBPL", "BPL status", AdmissionFormFieldType.Checkbox),
		Field("Eligibility.BPLCardNumber", "BPL card number"),
		Field("Eligibility.IncomeCertificateNumber", "Income certificate number"),
		Field("Eligibility.CasteCertificateNumber", "Caste certificate number"),
		// Identity and school records
		Field("Identity.BirthCertificateNumber", "Birth certificate number"),
		Field("Identity.BirthCertificateIssueDate", "Birth certificate issue date", AdmissionFormFieldType.Date),
		Field("Identity.ApaarId", "APAAR ID"),
		Field("Identity.PenNumber", "PEN number"),
		Field("Identity.UdiseStudentCode", "UDISE student code"),
		Field("Identity.DocumentName", "Name on identity document"),
		// More family details
		Field("Father.Email", "Father's email", AdmissionFormFieldType.Email),
		Field("Father.Qualification", "Father's qualification"),
		Field("Father.Designation", "Father's designation"),
		Field("Father.Employer", "Father's employer"),
		Field("Mother.Email", "Mother's email", AdmissionFormFieldType.Email),
		Field("Mother.Qualification", "Mother's qualification"),
		Field("Mother.Designation", "Mother's designation"),
		Field("Mother.Employer", "Mother's employer"),
		Field("Guardian.Occupation", "Guardian's occupation"),
		Field("Family.SiblingAtSchool", "Sibling at this school", AdmissionFormFieldType.Checkbox),
		// Permanent address
		Field("PermanentAddress.Line1", "Permanent address line 1"),
		Field("PermanentAddress.Line2", "Permanent address line 2"),
		Field("PermanentAddress.City", "Permanent address city"),
		Field("PermanentAddress.State", "Permanent address state"),
		Field("PermanentAddress.Country", "Permanent address country"),
		Field("PermanentAddress.PostalCode", "Permanent address postal code"),
		Field("PermanentAddress.SameAsCurrent", "Permanent address same as current", AdmissionFormFieldType.Checkbox),
		Field("Emergency.Relationship", "Emergency contact relationship"),
		Field("Address.DistanceFromSchool", "Distance from school (km)", AdmissionFormFieldType.Number),
		// More academic history
		Field("PreviousSchool.Address", "Previous school address", AdmissionFormFieldType.Textarea),
		Field("PreviousSchool.StudentId", "Previous school student ID"),
		Field("PreviousSchool.IndexNumber", "Previous school index number"),
		Field("PreviousSchool.Percentage", "Previous school marks percentage", AdmissionFormFieldType.Number),
		Field("PreviousSchool.ReasonForLeaving", "Reason for leaving previous school", AdmissionFormFieldType.Textarea),
		Field("PreviousSchool.TCIssueDate", "Transfer certificate issue date", AdmissionFormFieldType.Date),
		Field("Academic.SecondLanguage", "Second language"),
		Field("Academic.LearningSupportNeeds", "Learning support needs", AdmissionFormFieldType.Textarea),
		// More health information
		Field("Medical.HeightCm", "Height (cm)", AdmissionFormFieldType.Number),
		Field("Medical.WeightKg", "Weight (kg)", AdmissionFormFieldType.Number),
		Field("Medical.ImmunisationSummary", "Immunisation summary", AdmissionFormFieldType.Textarea),
		Field("Medical.DietaryRestrictions", "Dietary restrictions", AdmissionFormFieldType.Textarea),
		Field("Medical.HearingSupport", "Hearing support", AdmissionFormFieldType.Textarea),
		Field("Medical.VisionSupport", "Vision support", AdmissionFormFieldType.Textarea),
		Field("Medical.DoctorName", "Doctor's name"),
		Field("Medical.DoctorPhone", "Doctor's phone", AdmissionFormFieldType.Phone),
		// Admission preferences, retained with the Student as admission history
		Field("Preference.Class", "Preferred class"),
		Field("Preference.Board", "Preferred board"),
		Field("Preference.Medium", "Preferred medium"),
		Field("Preference.Campus", "Preferred campus"),
		Field("Preference.TransportRequired", "Transport needed", AdmissionFormFieldType.Checkbox),
		Field("Preference.PickupArea", "Preferred pickup area")
	];

	public static readonly IReadOnlyDictionary<string, AdmissionFormSystemFieldMetadataDto> Metadata =
		BuildMetadata();

	public static object? GetValue(string key, AdmissionApplicant applicant)
	{
		if (!Metadata.TryGetValue(key, out var field) ||
			!applicant.ExtraProperties.TryGetValue(PropertyPrefix + field.Key, out var raw) || raw is null)
			return null;
		var value = raw.ToString();
		return field.FieldType switch
		{
			AdmissionFormFieldType.Checkbox when bool.TryParse(value, out var boolean) => boolean,
			AdmissionFormFieldType.Number when decimal.TryParse(value, NumberStyles.Number,
				CultureInfo.InvariantCulture, out var number) => number,
			_ => value
		};
	}

	public static bool ApplyValue(string key, string value, AdmissionApplicant applicant)
	{
		if (!Metadata.TryGetValue(key, out var field)) return false;
		if (!string.IsNullOrWhiteSpace(value) && field.Key == "Applicant.ProfilePhoto") AdmissionStudentPhoto.Decode(value);
		if (!string.IsNullOrWhiteSpace(value) && field.Key == "Family.SiblingStudentIds") AdmissionStudentProfileMapping.ReadSiblings(value);
		if (string.IsNullOrWhiteSpace(value)) applicant.ExtraProperties.Remove(PropertyPrefix + field.Key);
		else applicant.SetProperty(PropertyPrefix + field.Key, value.Trim());
		return true;
	}

	public static void CopyToStudent(AdmissionApplicant applicant, Student student)
	{
		foreach (var key in Metadata.Keys)
		{
			if (HasCanonicalProtectedStorage(key))
			{
				student.ExtraProperties.Remove(PropertyPrefix + key);
				continue;
			}
			if (applicant.ExtraProperties.TryGetValue(PropertyPrefix + key, out var value) && value is not null)
				student.SetProperty(PropertyPrefix + key, value.ToString());
		}
	}

	public static IReadOnlyList<(string Key, string Label, string Value)> ReadStudent(Student student) =>
		Metadata.Values
			.Where(field => !HasCanonicalProtectedStorage(field.Key))
			.Select(field => (
				field.Key,
				field.DisplayName,
				Value: student.ExtraProperties.TryGetValue(PropertyPrefix + field.Key, out var raw)
					? field.Key == "Applicant.ProfilePhoto" ? "Photo uploaded" : raw?.ToString() : null))
			.Where(field => !string.IsNullOrWhiteSpace(field.Value))
			.Select(field => (field.Key, field.DisplayName, field.Value!))
			.ToList();

	private static bool HasCanonicalProtectedStorage(string key) => key is
		"Applicant.ProfilePhoto" or "Identity.AadhaarNumber" or "Identity.PanNumber" or
		"Identity.ApaarId" or "Identity.PenNumber" or "Identity.UdiseStudentCode";

	private static IReadOnlyDictionary<string, AdmissionFormSystemFieldMetadataDto> BuildMetadata()
	{
		var result = new Dictionary<string, AdmissionFormSystemFieldMetadataDto>(StringComparer.OrdinalIgnoreCase);
		foreach (var field in Fields) result.Add(field.Key, field);
		return result;
	}

	private static AdmissionFormSystemFieldMetadataDto Field(string key, string displayName,
		AdmissionFormFieldType fieldType = AdmissionFormFieldType.Text, string? optionsJson = null) => new()
	{
		Key = key,
		DisplayName = displayName,
		Description = "Saved with the applicant and carried into the Student record at admission finalization.",
		FieldType = fieldType,
		OptionsJson = optionsJson,
		ValidationJson = key switch
		{
			"Identity.AadhaarNumber" => "{\"format\":\"aadhaar\",\"regex\":\"^[2-9][0-9]{11}$\"}",
			"Identity.PanNumber" => "{\"format\":\"pan\",\"regex\":\"^[A-Z]{5}[0-9]{4}[A-Z]$\"}",
			"Applicant.ProfilePhoto" => "{\"format\":\"photo\"}",
			_ => null
		},
		Destination = AdmissionStudentProfileMapping.IsStructured(key) ? "studentProfile" : "capturedAdmissionDetails"
	};
}
