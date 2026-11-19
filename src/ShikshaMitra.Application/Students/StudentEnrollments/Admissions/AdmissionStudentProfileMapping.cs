using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Common;
using ShikshaMitra.Students.ValueObjects;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Maps accepted admission answers to supported profile storage. Call only for new
/// Students or within the owning Student Records review command.</summary>
internal static class AdmissionStudentProfileMapping
{
	private static readonly HashSet<string> StructuredKeys = new(StringComparer.OrdinalIgnoreCase)
	{
		"Applicant.ProfilePhoto", "Applicant.PlaceOfBirth", "Applicant.Nationality", "Applicant.MotherTongue",
		"Applicant.Religion", "Applicant.SocialCategory", "Applicant.Caste", "Applicant.HasDisability",
		"Applicant.DisabilityDetails", "Applicant.Email", "Contact.SecondaryPhone", "Address.AddressLine3",
		"Emergency.ContactName", "Emergency.ContactPhone", "Emergency.Relationship", "Identity.AadhaarNumber",
		"Identity.PanNumber", "Identity.ApaarId", "Identity.PenNumber", "Identity.UdiseStudentCode", "Identity.DocumentName", "Eligibility.IsBPL", "Eligibility.BPLCardNumber",
		"Family.IsSingleParentChild", "Family.IsSingleChild", "Family.SiblingStudentIds", "Academic.SecondLanguage",
		"PreviousSchool.Name", "PreviousSchool.Address", "PreviousSchool.Board", "PreviousSchool.Medium",
		"PreviousSchool.LastClass", "PreviousSchool.DateOfLeaving", "PreviousSchool.Percentage", "PreviousSchool.AcademicYear",
		"PreviousSchool.ReasonForLeaving", "PreviousSchool.TCNumber", "PreviousSchool.IndexNumber",
		"Medical.BloodGroup", "Medical.Allergies", "Medical.MedicalConditions", "Medical.CurrentMedication",
		"Medical.HeightCm", "Medical.WeightKg", "Medical.ImmunisationSummary",
		"PermanentAddress.Line1", "PermanentAddress.Line2", "PermanentAddress.City", "PermanentAddress.State",
		"PermanentAddress.Country", "PermanentAddress.PostalCode", "PermanentAddress.SameAsCurrent", "Guardian.Occupation"
	};

	public static bool IsStructured(string key) => StructuredKeys.Contains(key) ||
		new[] { "Father.", "Mother." }.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

	public static string? Read(AdmissionApplicant applicant, string key) =>
		applicant.ExtraProperties.TryGetValue("AdmissionStudentField." + key, out var value) ? value?.ToString() : null;

	public static void Apply(AdmissionApplicant applicant, Student student)
	{
		if (!applicant.ExtraProperties.Keys.Any(key => key.StartsWith("AdmissionStudentField.", StringComparison.Ordinal))) return;
		string? V(string key) => Read(applicant, key);
		bool? B(string key) => bool.TryParse(V(key), out var value) ? value : null;
		decimal? N(string key) => decimal.TryParse(V(key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
		DateTimeOffset? D(string key) => DateTimeOffset.TryParse(V(key), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value) ? value : null;
		var detail = student.AdditionalDetail;
		if (detail != null)
		{
			if (detail.DateOfBirth.HasValue && detail.Gender.HasValue)
				detail.UpdateGeneralInformation(detail.DateOfBirth.Value, detail.Gender.Value, detail.ProfilePictureUrl,
					detail.AdharNo, detail.ApparId, detail.PanNo, detail.PennNo,
					ParseEnum<ReligionEnum>(V("Applicant.Religion")) ?? detail.Religion, V("Applicant.Caste") ?? detail.FamilyCast,
					ParseEnum<CastCategoryEnum>(V("Applicant.SocialCategory")) ?? detail.CastCategory,
					V("Applicant.Nationality") ?? detail.Nationality, V("Applicant.PlaceOfBirth") ?? detail.BirthPlace,
					ParseEnum<MotherTongueEnum>(V("Applicant.MotherTongue")) ?? detail.MotherTongue,
					V("PreviousSchool.Name") ?? detail.PreviousSchool, V("PreviousSchool.LastClass") ?? detail.LastClassPassed,
					detail.StudentRegistrationState, detail.InquiryPreferenceId, detail.RegistrationFormNo ?? string.Empty);
			detail.ApplyAdmissionSupportDetails(V("Academic.SecondLanguage"), B("Applicant.HasDisability"),
				V("Applicant.DisabilityDetails"), V("Emergency.ContactName"), V("Emergency.Relationship"), V("Emergency.ContactPhone"));
			detail.ApplyAdmissionIdentityAndFamilyConditions(V("Identity.PanNumber"), B("Family.IsSingleChild"),
				B("Family.IsSingleParentChild"), B("Eligibility.IsBPL"), V("Eligibility.BPLCardNumber"));
		}
		var family = student.FamilyDetail;
		if (applicant.ExtraProperties.Keys.Any(key => new[] { "Father.", "Mother.", "Guardian.", "Family.SiblingStudentIds" }
			.Any(prefix => key.StartsWith("AdmissionStudentField." + prefix, StringComparison.Ordinal))))
			student.UpdateFamilyInfo(Parent("Father", family?.FatherDetails), Parent("Mother", family?.MotherDetails),
				Parent("Guardian", family?.GuardianDetails), ReadSiblings(V("Family.SiblingStudentIds")) ?? family?.SiblingStudentIds);
		AdmissionFamilyContactCapture.ApplySelection(applicant, student);
		var contact = student.ContactDetail;
		if (contact != null)
			student.ApplyVerifiedRegistrationContact(contact.PrimaryContactNo, V("Contact.SecondaryPhone") ?? contact.SecondaryContactNo,
				V("Applicant.Email") ?? contact.Email, contact.Address1, contact.Address2, V("Address.AddressLine3") ?? contact.Address3,
				contact.City, student.Id == applicant.ReservedStudentId ? StateValue(applicant.ExtraProperties.GetValueOrDefault("AdmissionLocation.Address.State")?.ToString()) ?? contact.State : contact.State,
				student.Id == applicant.ReservedStudentId ? CountryValue(applicant.ExtraProperties.GetValueOrDefault("AdmissionLocation.Address.Country")?.ToString()) ?? contact.Country : contact.Country, contact.Pincode);
		var communication = student.CommunicationDetail;
		if (applicant.ExtraProperties.Keys.Any(key => key.StartsWith("AdmissionStudentField.PermanentAddress.", StringComparison.Ordinal) ||
			key.StartsWith("AdmissionStudentField.Emergency.", StringComparison.Ordinal)))
		{
			var local = contact == null ? communication?.LocalAddress : new AddressDetails(contact.Address1, contact.Address2,
				contact.Address3, contact.City, contact.State, contact.Country, contact.Pincode);
			var same = B("PermanentAddress.SameAsCurrent") ?? communication?.IsPermanentSameAsLocal ?? false;
			var oldPermanent = communication?.PermanentAddress;
			var permanent = same ? local : new AddressDetails(V("PermanentAddress.Line1") ?? oldPermanent?.Address1,
				V("PermanentAddress.Line2") ?? oldPermanent?.Address2, oldPermanent?.Address3,
				LocationName(V("PermanentAddress.City")) ?? oldPermanent?.City,
				StateValue(V("PermanentAddress.State")) ?? oldPermanent?.State,
				CountryValue(V("PermanentAddress.Country")) ?? oldPermanent?.Country,
				V("PermanentAddress.PostalCode") ?? oldPermanent?.Pincode);
			student.ApplyVerifiedRegistrationCommunication(local, permanent, same, V("Emergency.ContactName") ?? communication?.EmergencyContactName,
				V("Emergency.ContactPhone") ?? communication?.EmergencyContactPhone, V("Emergency.Relationship") ?? communication?.EmergencyContactRelation,
				communication?.EmergencyAddress, communication?.IsEmergencySameAsLocal ?? true);
		}
		var medical = student.MedicalRecord;
		var medicalMeasurements = (medical?.BodyMassIndex, medical?.PulseRate, medical?.Hemoglobin,
			medical?.ChildImmunization, medical?.CovidVaccinationDoses, medical?.ImmunizationRemarks);
		if (medical != null || applicant.ExtraProperties.Keys.Any(key => key.StartsWith("AdmissionStudentField.Medical.", StringComparison.Ordinal)))
		{
			student.ApplyVerifiedRegistrationMedical(N("Medical.WeightKg") ?? medical?.WeightKg, N("Medical.HeightCm") ?? medical?.HeightCm,
				V("Medical.Allergies") ?? medical?.Allergies, V("Medical.BloodGroup") ?? medical?.BloodGroup,
				V("Medical.MedicalConditions") ?? medical?.ChronicConditions, V("Medical.CurrentMedication") ?? medical?.CurrentMedications);
			medical = student.MedicalRecord!;
			medical.UpdateMedicalRecords(medical.WeightKg, medical.HeightCm, medicalMeasurements.Item1, medicalMeasurements.Item2, medicalMeasurements.Item3,
				medical.Allergies, V("Medical.ImmunisationSummary") ?? medicalMeasurements.Item4, medicalMeasurements.Item5,
				medicalMeasurements.Item6, medical.BloodGroup, medical.ChronicConditions, medical.CurrentMedications);
		}
		var school = student.PreviousSchoolDetail;
		var schoolReferences = (school?.SchoolIndexNumber, school?.TCNumber, school?.Remarks);
		if (school != null || applicant.ExtraProperties.Keys.Any(key => key.StartsWith("AdmissionStudentField.PreviousSchool.", StringComparison.Ordinal)))
		{
			student.ApplyVerifiedRegistrationPreviousSchool(V("PreviousSchool.Name") ?? school?.SchoolName,
				V("PreviousSchool.Address") ?? school?.SchoolAddress, V("PreviousSchool.Board") ?? school?.Board,
				V("PreviousSchool.Medium") ?? school?.Medium, V("PreviousSchool.LastClass") ?? school?.LastClassAttended,
				D("PreviousSchool.DateOfLeaving") ?? school?.DateOfLeaving, N("PreviousSchool.Percentage") ?? school?.PercentageObtained,
				V("PreviousSchool.AcademicYear") ?? school?.AcademicYear, V("PreviousSchool.ReasonForLeaving") ?? school?.ReasonForLeaving);
			school = student.PreviousSchoolDetail!;
			school.UpdatePreviousSchoolDetails(school.SchoolName, school.SchoolAddress, school.Board, school.Medium,
				school.LastClassAttended, V("PreviousSchool.IndexNumber") ?? schoolReferences.Item1,
				V("PreviousSchool.TCNumber") ?? schoolReferences.Item2, school.DateOfLeaving, school.PercentageObtained,
				school.AcademicYear, school.ReasonForLeaving, schoolReferences.Item3);
		}

		ParentGuardianDetails? Parent(string prefix, ParentGuardianDetails? previous)
		{
			if (!applicant.ExtraProperties.Keys.Any(key => key.StartsWith("AdmissionStudentField." + prefix + ".", StringComparison.OrdinalIgnoreCase))) return previous;
			return new(V(prefix + ".Name") ?? previous?.FullName, previous?.DateOfBirth,
				V(prefix + ".Occupation") ?? previous?.Occupation, V(prefix + ".Email") ?? previous?.Email,
				V(prefix + ".Mobile") ?? previous?.ContactNo, previous?.OfficeNo,
				V(prefix + ".Qualification") ?? previous?.Qualification, previous?.YearOfPassing,
				V(prefix + ".Designation") ?? previous?.Designation, V(prefix + ".Employer") ?? previous?.Organization,
				previous?.AadharNo, CopyAddress(previous?.OfficeAddress), CopyAddress(previous?.LocalAddress));
		}
	}

	public static List<Guid>? ReadSiblings(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return null;
		try
		{
			var result = JsonSerializer.Deserialize<List<Guid>>(value)?.Distinct().ToList();
			if (result == null || result.Count > 20 || result.Contains(Guid.Empty))
				throw new Volo.Abp.BusinessException("Admissions:InvalidSiblingSelection");
			return result;
		}
		catch (JsonException exception) { throw new Volo.Abp.BusinessException("Admissions:InvalidSiblingSelection", innerException: exception); }
	}

	private static T? ParseEnum<T>(string? value) where T : struct, Enum =>
		Enum.TryParse<T>(value, true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
	private static AddressDetails? CopyAddress(AddressDetails? address) => address == null ? null : new(
		address.Address1, address.Address2, address.Address3, address.City,
		address.State == null ? null : new State(address.State.Name, address.State.IsoCode),
		address.Country == null ? null : new Country(address.Country.Name, address.Country.IsoCode), address.Pincode);
	internal static string? LocationName(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return null;
		try
		{
			using var json = JsonDocument.Parse(value);
			return json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("name", out var name)
				? name.GetString() : value;
		}
		catch (JsonException) { return value; }
	}
	internal static State? StateValue(string? value) => LocationName(value) is { } name ? new State(name, LocationIsoCode(value)) : null;
	internal static Country? CountryValue(string? value) => LocationName(value) is { } name ? new Country(name, LocationIsoCode(value)) : null;
	private static string LocationIsoCode(string? value)
	{
		try
		{
			using var json = JsonDocument.Parse(value ?? string.Empty);
			return json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("isoCode", out var iso)
				? iso.GetString() ?? string.Empty : string.Empty;
		}
		catch (JsonException) { return string.Empty; }
	}
}
