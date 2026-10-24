using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionFormBindingRegistry
{
	public static readonly IReadOnlyDictionary<string, AdmissionFormSystemFieldMetadataDto> Metadata =
		BuildMetadata();

	private static IReadOnlyDictionary<string, AdmissionFormSystemFieldMetadataDto> BuildMetadata()
	{
		var metadata = new Dictionary<string, AdmissionFormSystemFieldMetadataDto>(StringComparer.OrdinalIgnoreCase)
		{
			["Applicant.FirstName"] = new() { Key = "Applicant.FirstName", DisplayName = "Applicant first name", Description = "The applicant's first name.", FieldType = AdmissionFormFieldType.Text },
			["Applicant.MiddleName"] = new() { Key = "Applicant.MiddleName", DisplayName = "Applicant middle name", Description = "The applicant's middle name.", FieldType = AdmissionFormFieldType.Text },
			["Applicant.LastName"] = new() { Key = "Applicant.LastName", DisplayName = "Applicant last name", Description = "The applicant's last name.", FieldType = AdmissionFormFieldType.Text },
			["Applicant.DateOfBirth"] = new() { Key = "Applicant.DateOfBirth", DisplayName = "Applicant date of birth", Description = "The applicant's date of birth.", FieldType = AdmissionFormFieldType.Date },
			["Applicant.Gender"] = new() { Key = "Applicant.Gender", DisplayName = "Applicant gender", Description = "The applicant's gender.", FieldType = AdmissionFormFieldType.Select, OptionsJson = "[{\"value\":\"Male\",\"label\":\"Male\"},{\"value\":\"Female\",\"label\":\"Female\"}]" },
			["Guardian.FirstName"] = new() { Key = "Guardian.FirstName", DisplayName = "Guardian first name", Description = "The primary guardian's first name.", FieldType = AdmissionFormFieldType.Text },
			["Guardian.LastName"] = new() { Key = "Guardian.LastName", DisplayName = "Guardian last name", Description = "The primary guardian's last name.", FieldType = AdmissionFormFieldType.Text },
			["Guardian.Mobile"] = new() { Key = "Guardian.Mobile", DisplayName = "Guardian mobile", Description = "The primary guardian's mobile number.", FieldType = AdmissionFormFieldType.Phone },
			["Guardian.Email"] = new() { Key = "Guardian.Email", DisplayName = "Guardian email", Description = "The primary guardian's email address.", FieldType = AdmissionFormFieldType.Email },
			["Address.AddressLine1"] = new() { Key = "Address.AddressLine1", DisplayName = "Address line 1", Description = "The applicant's primary address line.", FieldType = AdmissionFormFieldType.Text },
			["Address.AddressLine2"] = new() { Key = "Address.AddressLine2", DisplayName = "Address line 2", Description = "The applicant's secondary address line.", FieldType = AdmissionFormFieldType.Text },
			["Address.City"] = new() { Key = "Address.City", DisplayName = "City", Description = "The applicant's city.", FieldType = AdmissionFormFieldType.Text },
			["Address.State"] = new() { Key = "Address.State", DisplayName = "State", Description = "The applicant's state or province.", FieldType = AdmissionFormFieldType.Text },
			["Address.Country"] = new() { Key = "Address.Country", DisplayName = "Country", Description = "The applicant's country.", FieldType = AdmissionFormFieldType.Text },
			["Address.PostalCode"] = new() { Key = "Address.PostalCode", DisplayName = "Postal code", Description = "The applicant's postal or ZIP code.", FieldType = AdmissionFormFieldType.Text }
		};
		foreach (var field in AdmissionAdditionalStudentFieldCatalog.Metadata.Values)
			metadata.Add(field.Key, field);
		return metadata;
	}

	public static object? GetValue(string key, AdmissionApplicant applicant, AdmissionGuardian? guardian)
	{
		var confirmed = AdmissionSiblingParentSelection.ReadConfirmation(applicant);
		if (confirmed != null && key is "Guardian.FirstName" or "Guardian.LastName" or "Guardian.Mobile" or "Guardian.Email")
		{
			var prefix = confirmed.Relationship == "father" ? "Father" : "Mother";
			var name = confirmed.Name ?? AdmissionStudentProfileMapping.Read(applicant, prefix + ".Name") ?? string.Empty;
			var parts = name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
			return key switch
			{
				"Guardian.FirstName" => confirmed.FirstName ?? (parts.Length > 0 ? parts[0] : string.Empty),
				"Guardian.LastName" => confirmed.LastName ?? (parts.Length > 1 ? parts[1] : null),
				"Guardian.Mobile" => confirmed.Mobile ?? AdmissionStudentProfileMapping.Read(applicant, prefix + ".Mobile"),
				_ => confirmed.Email
			};
		}
		return key switch
		{
			"Applicant.FirstName" => applicant.FirstName,
			"Applicant.MiddleName" => applicant.MiddleName,
			"Applicant.LastName" => applicant.LastName,
			"Applicant.DateOfBirth" => applicant.DateOfBirth,
			"Applicant.Gender" => applicant.Gender,
			"Guardian.FirstName" => guardian?.FirstName,
			"Guardian.LastName" => guardian?.LastName,
			"Guardian.Mobile" => guardian?.Mobile,
			"Guardian.Email" => guardian?.Email,
			"Address.AddressLine1" => applicant.AddressLine1,
			"Address.AddressLine2" => applicant.AddressLine2,
			"Address.City" => applicant.City,
			"Address.State" => applicant.State,
			"Address.Country" => applicant.Country,
			"Address.PostalCode" => applicant.PostalCode,
			_ => AdmissionAdditionalStudentFieldCatalog.GetValue(key, applicant)
		};
	}

	public static bool ApplyValue(string key, string value, AdmissionApplicant applicant, AdmissionGuardian? guardian)
	{
		if (key is "Address.State" or "Address.Country") applicant.SetProperty("AdmissionLocation." + key, value);
		switch (key)
		{
			case "Applicant.FirstName": UpdateApplicant(applicant, value, applicant.MiddleName, applicant.LastName, applicant.DateOfBirth, applicant.Gender); return true;
			case "Applicant.MiddleName": UpdateApplicant(applicant, applicant.FirstName, value, applicant.LastName, applicant.DateOfBirth, applicant.Gender); return true;
			case "Applicant.LastName": UpdateApplicant(applicant, applicant.FirstName, applicant.MiddleName, value, applicant.DateOfBirth, applicant.Gender); return true;
			case "Applicant.DateOfBirth":
				if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)) return false;
				UpdateApplicant(applicant, applicant.FirstName, applicant.MiddleName, applicant.LastName, date.Date, applicant.Gender); return true;
			case "Applicant.Gender": UpdateApplicant(applicant, applicant.FirstName, applicant.MiddleName, applicant.LastName, applicant.DateOfBirth, value); return true;
			case "Guardian.FirstName": if (guardian is null) return false; guardian.UpdateContact(value, guardian.LastName, guardian.Mobile, guardian.Email); return true;
			case "Guardian.LastName": if (guardian is null) return false; guardian.UpdateContact(guardian.FirstName, value, guardian.Mobile, guardian.Email); return true;
			case "Guardian.Mobile": if (guardian is null) return false; guardian.UpdateContact(guardian.FirstName, guardian.LastName, value, guardian.Email); return true;
			case "Guardian.Email": if (guardian is null) return false; guardian.UpdateContact(guardian.FirstName, guardian.LastName, guardian.Mobile, value); return true;
			case "Address.AddressLine1": UpdateApplicantAddress(applicant, value, applicant.AddressLine2, applicant.City, applicant.State, applicant.Country, applicant.PostalCode); return true;
			case "Address.AddressLine2": UpdateApplicantAddress(applicant, applicant.AddressLine1, value, applicant.City, applicant.State, applicant.Country, applicant.PostalCode); return true;
			case "Address.City": UpdateApplicantAddress(applicant, applicant.AddressLine1, applicant.AddressLine2, ReadLocationName(value), applicant.State, applicant.Country, applicant.PostalCode); return true;
			case "Address.State": UpdateApplicantAddress(applicant, applicant.AddressLine1, applicant.AddressLine2, applicant.City, ReadLocationName(value), applicant.Country, applicant.PostalCode); return true;
			case "Address.Country": UpdateApplicantAddress(applicant, applicant.AddressLine1, applicant.AddressLine2, applicant.City, applicant.State, ReadLocationName(value), applicant.PostalCode); return true;
			case "Address.PostalCode": UpdateApplicantAddress(applicant, applicant.AddressLine1, applicant.AddressLine2, applicant.City, applicant.State, applicant.Country, value); return true;
			default: return AdmissionAdditionalStudentFieldCatalog.ApplyValue(key, value, applicant);
		}
	}

	private static void UpdateApplicant(AdmissionApplicant applicant, string firstName, string? middleName, string? lastName, DateTime? dateOfBirth, string? gender) =>
		applicant.UpdateProfile(firstName, middleName, lastName, dateOfBirth, gender, applicant.AddressLine1, applicant.AddressLine2, applicant.City, applicant.State, applicant.Country, applicant.PostalCode);

	private static void UpdateApplicantAddress(AdmissionApplicant applicant, string? addressLine1, string? addressLine2, string? city, string? state, string? country, string? postalCode) =>
		applicant.UpdateProfile(applicant.FirstName, applicant.MiddleName, applicant.LastName, applicant.DateOfBirth, applicant.Gender, addressLine1, addressLine2, city, state, country, postalCode);

	private static string ReadLocationName(string value)
	{
		try
		{
			using var document = JsonDocument.Parse(value);
			return document.RootElement.ValueKind == JsonValueKind.Object &&
				document.RootElement.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
				? name.GetString() ?? value
				: value;
		}
		catch (JsonException) { return value; }
	}
}
