using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.Students.ValueObjects;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmVerifiedRegistrationStudentProjector : ITransientDependency
{
	public void Project(Student student, AdmissionCrmRegistrationApplication registration)
	{
		if (registration.Status is not (AdmissionCrmRegistrationStatus.Verified or AdmissionCrmRegistrationStatus.AdmissionReady))
		{
			return;
		}

		var values = ReadValues(registration.PrefillJson);
		foreach (var (key, value) in ReadValues(registration.ResponseJson)) values[key] = value;

		student.UpdateGeneralInformation(
			Text(values, "firstName") ?? student.FirstName,
			Text(values, "middleName") ?? student.MiddleName,
			Text(values, "lastName") ?? student.LastName);

		var dateOfBirth = Date(values, "dateOfBirth") ?? student.AdditionalDetail?.DateOfBirth;
		var gender = EnumValue<GenderEnum>(values, "gender") ?? student.AdditionalDetail?.Gender;
		student.ApplyVerifiedRegistrationAdditionalDetails(dateOfBirth, gender, Text(values, "aadharNo"),
			Text(values, "apparId"), Text(values, "penNo"), EnumValue<ReligionEnum>(values, "religion"),
			Text(values, "familyCast"), EnumValue<CastCategoryEnum>(values, "castCategory"),
			Text(values, "nationality"), Text(values, "birthPlace"),
			EnumValue<MotherTongueEnum>(values, "motherTongue"), Text(values, "previousSchoolName"),
			Text(values, "lastClassAttended"), registration.FormNumber, Text(values, "secondLanguage"),
			Boolean(values, "haveDisability"), Text(values, "disabilityDetails"),
			Text(values, "emergencyContactName"), Text(values, "emergencyContactRelation"),
			Text(values, "emergencyContactNo"), EnumValue<Category>(values, "category"));

		var primaryContact = Text(values, "primaryContactNo") ?? student.ContactDetail?.PrimaryContactNo ?? string.Empty;
		student.ApplyVerifiedRegistrationContact(primaryContact, Text(values, "secondaryContactNo"),
			Text(values, "email") ?? student.ContactDetail?.Email, Text(values, "localAddress1"),
			Text(values, "localAddress2"), Text(values, "localAddress3"), Text(values, "localCity"),
			State(values, "localState"), Country(values, "localCountry"), Text(values, "localPincode"));

		var localAddress = Address(values, "local");
		var sameAddress = Boolean(values, "permanentSameAsLocal") ?? false;
		student.ApplyVerifiedRegistrationCommunication(localAddress, sameAddress ? localAddress : Address(values, "permanent"),
			sameAddress, Text(values, "emergencyContactName"), Text(values, "emergencyContactNo"),
			Text(values, "emergencyContactRelation"));

		var guardianName = Text(values, "guardianName") ?? Text(values, "parentGuardianName");
		if (!string.IsNullOrWhiteSpace(guardianName))
		{
			student.UpdateFamilyInfo(null, null,
				new ParentGuardianDetails(guardianName, null, Text(values, "guardianOccupation"),
					Text(values, "guardianEmail"), Text(values, "guardianContactNo") ?? primaryContact,
					null, Text(values, "guardianQualification"), null, null, null,
					Text(values, "guardianAadharNo"), null, localAddress), null);
		}

		student.ApplyVerifiedRegistrationPreviousSchool(Text(values, "previousSchoolName"),
			Text(values, "previousSchoolAddress"), Text(values, "previousSchoolBoard"),
			Text(values, "previousSchoolMedium"), Text(values, "lastClassAttended"),
			Date(values, "previousSchoolLeavingDate"), Decimal(values, "previousSchoolPercentage"),
			Text(values, "previousSchoolAcademicYear"), Text(values, "previousSchoolLeavingReason"));
		student.ApplyVerifiedRegistrationMedical(Decimal(values, "weightKg"), Decimal(values, "heightCm"),
			Text(values, "allergies"), Text(values, "bloodGroup"), Text(values, "chronicConditions"),
			Text(values, "currentMedications"));
		student.ApplyVerifiedRegistrationBank(Text(values, "bankName"), Text(values, "bankAccountNo"),
			Text(values, "bankIfscCode"), Text(values, "bankBranchName"),
			Text(values, "bankAccountHolderName"), Text(values, "bankAccountType"));
	}

	private static Dictionary<string, JsonElement> ReadValues(string json)
	{
		using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
		var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
		if (document.RootElement.ValueKind != JsonValueKind.Object) return result;
		foreach (var property in document.RootElement.EnumerateObject()) result[property.Name] = property.Value.Clone();
		return result;
	}

	private static string? Text(IReadOnlyDictionary<string, JsonElement> values, string key) =>
		values.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;

	private static DateTimeOffset? Date(IReadOnlyDictionary<string, JsonElement> values, string key) =>
		DateTimeOffset.TryParse(Text(values, key), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;

	private static decimal? Decimal(IReadOnlyDictionary<string, JsonElement> values, string key) =>
		values.TryGetValue(key, out var value) && value.TryGetDecimal(out var result) ? result : null;

	private static bool? Boolean(IReadOnlyDictionary<string, JsonElement> values, string key) =>
		values.TryGetValue(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

	private static TEnum? EnumValue<TEnum>(IReadOnlyDictionary<string, JsonElement> values, string key) where TEnum : struct, Enum =>
		Enum.TryParse<TEnum>(Text(values, key), true, out var value) ? value : null;

	private static AddressDetails? Address(IReadOnlyDictionary<string, JsonElement> values, string prefix)
	{
		var address1 = Text(values, $"{prefix}Address1");
		if (string.IsNullOrWhiteSpace(address1)) return null;
		return new AddressDetails(address1, Text(values, $"{prefix}Address2"), Text(values, $"{prefix}Address3"),
			Text(values, $"{prefix}City"), State(values, $"{prefix}State"), Country(values, $"{prefix}Country"),
			Text(values, $"{prefix}Pincode"));
	}

	private static State? State(IReadOnlyDictionary<string, JsonElement> values, string key)
	{
		var name = Text(values, key);
		return string.IsNullOrWhiteSpace(name) ? null : new State(name, Text(values, $"{key}Code") ?? name);
	}

	private static Country? Country(IReadOnlyDictionary<string, JsonElement> values, string key)
	{
		var name = Text(values, key);
		return string.IsNullOrWhiteSpace(name) ? null : new Country(name, Text(values, $"{key}Code") ?? name);
	}
}
