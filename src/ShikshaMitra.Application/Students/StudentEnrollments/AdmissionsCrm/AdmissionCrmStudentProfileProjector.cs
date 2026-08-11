using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Students.ValueObjects;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

/// <summary>
/// Projects the verified registration snapshot into the canonical student
/// aggregate before enrollment is activated.
/// </summary>
public class AdmissionCrmStudentProfileProjector(
	IRepository<AdmissionCrmRegistrationApplication, Guid> applicationRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<bool> ProjectAsync(Student student, AdmissionInquiryLead lead, GenderEnum gender)
	{
		var query = (await applicationRepository.GetQueryableAsync()).Where(item =>
			item.TenantId == CurrentTenant.Id && item.LeadId == lead.Id &&
			item.Status != AdmissionCrmRegistrationStatus.Revoked &&
			item.Status != AdmissionCrmRegistrationStatus.Withdrawn);
		var application = await AsyncExecuter.FirstOrDefaultAsync(query
			.OrderByDescending(item => item.VerifiedAt)
			.ThenByDescending(item => item.SubmittedAt)
			.ThenByDescending(item => item.CreationTime));
		if (application == null) return false;

		var values = AdmissionCrmRegistrationModelMapper.Deserialize<Dictionary<string, JsonElement>>(application.PrefillJson);
		foreach (var item in AdmissionCrmRegistrationModelMapper.Deserialize<Dictionary<string, JsonElement>>(application.ResponseJson))
			values[item.Key] = item.Value;

		var firstName = Get(values, "firstName") ?? lead.FirstName;
		var middleName = Get(values, "middleName") ?? lead.MiddleName;
		var lastName = Get(values, "lastName") ?? lead.LastName;
		student.UpdateGeneralInformation(firstName, middleName, lastName);

		var contact = student.ContactDetail;
		contact?.UpdateContactInfo(
			Get(values, "primaryContactNo") ?? lead.PrimaryContactNo ?? contact.PrimaryContactNo,
			Get(values, "secondaryContactNo") ?? contact.SecondaryContactNo,
			Get(values, "email") ?? lead.Email ?? contact.Email,
			Get(values, "address1", "addressLine1") ?? contact.Address1,
			Get(values, "address2", "addressLine2") ?? contact.Address2,
			Get(values, "address3", "addressLine3") ?? contact.Address3,
			Get(values, "city") ?? contact.City,
			BuildState(values) ?? contact.State,
			BuildCountry(values) ?? contact.Country,
			Get(values, "pincode", "pinCode") ?? contact.Pincode);

		student.AdditionalDetail?.UpdateGeneralInformation(
			lead.DateOfBirth!.Value,
			gender,
			Get(values, "profilePictureUrl"),
			null,
			Get(values, "apaarId", "apparId"),
			Get(values, "panNo"),
			Get(values, "penNo", "pennNo"),
			ParseEnum<ReligionEnum>(values, "religion"),
			Get(values, "familyCast", "caste") ?? "Not specified",
			ParseEnum<CastCategoryEnum>(values, "castCategory", "casteCategory"),
			Get(values, "nationality"),
			Get(values, "birthPlace"),
			ParseEnum<MotherTongueEnum>(values, "motherTongue"),
			Get(values, "previousSchool", "previousSchoolName"),
			Get(values, "lastClassPassed"),
			StudentRegistrationStateEnum.General,
			lead.InquirySourceId,
			application.FormNumber);

		var father = BuildParent(values, "father");
		var mother = BuildParent(values, "mother");
		var guardian = BuildParent(values, "guardian",
			Get(values, "parentGuardianName") ?? lead.ParentGuardianName,
			lead.Email,
			lead.PrimaryContactNo);
		if (father != null || mother != null || guardian != null)
			student.UpdateFamilyInfo(father, mother, guardian, null);
		return true;
	}

	private static ParentGuardianDetails? BuildParent(
		IReadOnlyDictionary<string, JsonElement> values,
		string prefix,
		string? fallbackName = null,
		string? fallbackEmail = null,
		string? fallbackContact = null)
	{
		var name = Get(values, prefix + "Name", prefix + "FullName") ?? fallbackName;
		var email = Get(values, prefix + "Email") ?? fallbackEmail;
		var contact = Get(values, prefix + "ContactNo", prefix + "Phone") ?? fallbackContact;
		if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(contact)) return null;
		return new ParentGuardianDetails(name, null, Get(values, prefix + "Occupation"), email, contact,
			Get(values, prefix + "OfficeNo"), Get(values, prefix + "Qualification"), null,
			Get(values, prefix + "Designation"), Get(values, prefix + "Organization"), null, null, null);
	}

	private static State? BuildState(IReadOnlyDictionary<string, JsonElement> values)
	{
		var name = Get(values, "state", "stateName");
		return string.IsNullOrWhiteSpace(name) ? null : new State(name, Get(values, "stateIsoCode") ?? name);
	}

	private static Country? BuildCountry(IReadOnlyDictionary<string, JsonElement> values)
	{
		var name = Get(values, "country", "countryName");
		return string.IsNullOrWhiteSpace(name) ? null : new Country(name, Get(values, "countryIsoCode") ?? name);
	}

	private static TEnum? ParseEnum<TEnum>(IReadOnlyDictionary<string, JsonElement> values, params string[] keys)
		where TEnum : struct, Enum =>
		Enum.TryParse<TEnum>(Get(values, keys), true, out var result) ? result : null;

	private static string? Get(IReadOnlyDictionary<string, JsonElement> values, params string[] keys)
	{
		foreach (var key in keys)
		{
			var item = values.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
			if (string.IsNullOrWhiteSpace(item.Key)) continue;
			var value = item.Value.ValueKind == JsonValueKind.String ? item.Value.GetString() : item.Value.GetRawText();
			if (!string.IsNullOrWhiteSpace(value) && value != "null") return value.Trim();
		}
		return null;
	}
}
