using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;

namespace ShikshaMitra.HR.Employees.Onboarding;

public static class PublicEmployeeApplicationSubmitValidation
{
	private const int StateIsoCodeMaxLength = 3;
	private const int CountryIsoCodeMaxLength = 3;
	private static readonly Regex IndianMobileRegex = new(@"^[6-9]\d{9}$", RegexOptions.Compiled);
	private static readonly Regex PincodeRegex = new(@"^\d{6}$", RegexOptions.Compiled);

	public static IReadOnlyList<ValidationResult> Validate(
		PublicEmployeeApplicationSubmitDto? input,
		DateOnly currentDate)
	{
		var results = new List<ValidationResult>();
		if (input == null)
		{
			Add(results, "Application data is required.", "data");
			return results;
		}

		ValidatePersonalInfo(input.PersonalInfo, currentDate, results);
		ValidateFamilyInfo(input.FamilyInfo, results);
		ValidateAdditionalInfo(input.AdditionalInfo, results);
		ValidateWorkExperiences(input.WorkExperiences, currentDate, results);

		return results;
	}

	private static void ValidatePersonalInfo(
		EmployeeApplicationPersonalInfoDto? personalInfo,
		DateOnly currentDate,
		List<ValidationResult> results)
	{
		if (personalInfo == null)
		{
			Add(results, "Personal information is required.", "personalInfo");
			return;
		}

		Required(results, personalInfo.FirstName, "personalInfo.firstName", "First name is required.");
		Max(results, personalInfo.FirstName, 256, "personalInfo.firstName", "First name");
		Max(results, personalInfo.MiddleName, 256, "personalInfo.middleName", "Middle name");
		Max(results, personalInfo.ShortName, 128, "personalInfo.shortName", "Short name");
		Required(results, personalInfo.LastName, "personalInfo.lastName", "Last name is required.");
		Max(results, personalInfo.LastName, 256, "personalInfo.lastName", "Last name");

		Required(results, personalInfo.Email, "personalInfo.email", "Email is required.");
		Max(results, personalInfo.Email, 256, "personalInfo.email", "Email");
		if (!string.IsNullOrWhiteSpace(personalInfo.Email) &&
		    !new EmailAddressAttribute().IsValid(personalInfo.Email))
		{
			Add(results, "Enter a valid email address.", "personalInfo.email");
		}

		Required(results, personalInfo.MobileNo, "personalInfo.mobileNo", "Mobile number is required.");
		ValidateIndianMobile(results, personalInfo.MobileNo, "personalInfo.mobileNo", required: true);
		ValidateIndianMobile(results, personalInfo.SecondaryMobileNo, "personalInfo.secondaryMobileNo");
		if (!string.IsNullOrWhiteSpace(personalInfo.SecondaryMobileNo) &&
		    NormalizeDigits(personalInfo.MobileNo) == NormalizeDigits(personalInfo.SecondaryMobileNo))
		{
			Add(results, "Secondary mobile number must be different from the primary mobile number.", "personalInfo.secondaryMobileNo");
		}

		if (!personalInfo.DateOfBirth.HasValue)
		{
			Add(results, "Date of birth is required.", "personalInfo.dateOfBirth");
		}
		else
		{
			var dob = DateOnly.FromDateTime(personalInfo.DateOfBirth.Value);
			if (dob >= currentDate)
			{
				Add(results, "Date of birth must be in the past.", "personalInfo.dateOfBirth");
			}
			else if (CalculateAge(dob, currentDate) < 18)
			{
				Add(results, "Applicant must be at least 18 years old.", "personalInfo.dateOfBirth");
			}
		}

		RequiredEnum(results, personalInfo.Gender, 1, 2, "personalInfo.gender", "Gender is required.");
		RequiredEnum(results, personalInfo.CasteType, 0, 3, "personalInfo.casteType", "Category is required.");
		RequiredEnum(results, personalInfo.MaritalStatus, 0, 3, "personalInfo.maritalStatus", "Marital status is required.");
		OptionalEnum(results, personalInfo.BloodGroup, 1, 8, "personalInfo.bloodGroup", "Blood group");
		Required(results, personalInfo.Religion, "personalInfo.religion", "Religion is required.");
		Max(results, personalInfo.Religion, 128, "personalInfo.religion", "Religion");
		Required(results, personalInfo.FamilyCaste, "personalInfo.familyCaste", "Family caste is required.");
		Max(results, personalInfo.FamilyCaste, 128, "personalInfo.familyCaste", "Family caste");
		Max(results, personalInfo.Department, 256, "personalInfo.department", "Department");
		Max(results, personalInfo.Designation, 256, "personalInfo.designation", "Designation");
		Max(results, personalInfo.Qualification, 512, "personalInfo.qualification", "Qualification");
		Max(results, personalInfo.IdentificationMark1, 256, "personalInfo.identificationMark1", "Identification mark");

		Required(results, personalInfo.Address1, "personalInfo.address1", "Address line 1 is required.");
		Required(results, personalInfo.Address2, "personalInfo.address2", "Address line 2 is required.");
		Max(results, personalInfo.Address1, 512, "personalInfo.address1", "Address line 1");
		Max(results, personalInfo.Address2, 512, "personalInfo.address2", "Address line 2");
		Max(results, personalInfo.Address3, 512, "personalInfo.address3", "Address line 3");
		ValidateLocation(results, personalInfo.Country, "personalInfo.country", "Country", required: true, CountryIsoCodeMaxLength);
		ValidateLocation(results, personalInfo.State, "personalInfo.state", "State", required: true, StateIsoCodeMaxLength);
		Required(results, personalInfo.City, "personalInfo.city", "City is required.");
		Max(results, personalInfo.City, 128, "personalInfo.city", "City");
		ValidatePincode(results, personalInfo.Pincode, "personalInfo.pincode");
	}

	private static void ValidateFamilyInfo(
		EmployeeApplicationFamilyInfoDto? familyInfo,
		List<ValidationResult> results)
	{
		if (familyInfo == null)
		{
			return;
		}

		ValidateFamilyMember(familyInfo.FatherDetails, "familyInfo.fatherDetails", "Father", results);
		ValidateFamilyMember(familyInfo.MotherDetails, "familyInfo.motherDetails", "Mother", results);
		ValidateFamilyMember(familyInfo.SpouseDetails, "familyInfo.spouseDetails", "Spouse", results);
	}

	private static void ValidateFamilyMember(
		EmployeeApplicationFamilyMemberDto? member,
		string path,
		string label,
		List<ValidationResult> results)
	{
		if (member == null)
		{
			return;
		}

		if (IsEmptyFamilyMember(member))
		{
			Add(results, $"{label} details are empty. Remove the row or enter the name.", $"{path}.name");
			return;
		}

		Required(results, member.Name, $"{path}.name", $"{label} name is required.");
		Max(results, member.Name, 256, $"{path}.name", $"{label} name");
		Max(results, member.Occupation, 256, $"{path}.occupation", $"{label} occupation");
		ValidateIndianMobile(results, member.MobileNo, $"{path}.mobileNo");
		Max(results, member.Address1, 512, $"{path}.address1", $"{label} address line 1");
		Max(results, member.Address2, 512, $"{path}.address2", $"{label} address line 2");
		Max(results, member.Address3, 512, $"{path}.address3", $"{label} address line 3");
		Max(results, member.City, 128, $"{path}.city", $"{label} city");
		ValidatePincode(results, member.Pincode, $"{path}.pincode");
		ValidateLocation(results, member.Country, $"{path}.country", $"{label} country", required: false, CountryIsoCodeMaxLength);
		ValidateLocation(results, member.State, $"{path}.state", $"{label} state", required: false, StateIsoCodeMaxLength);
	}

	private static void ValidateAdditionalInfo(
		EmployeeApplicationAdditionalInfoDto? additionalInfo,
		List<ValidationResult> results)
	{
		if (additionalInfo == null)
		{
			return;
		}

		Max(results, additionalInfo.SecondLanguage, 50, "additionalInfo.secondLanguage", "Second language");
	}

	private static void ValidateWorkExperiences(
		IReadOnlyList<EmployeeApplicationWorkExperienceDto>? workExperiences,
		DateOnly currentDate,
		List<ValidationResult> results)
	{
		if (workExperiences == null)
		{
			return;
		}

		for (var i = 0; i < workExperiences.Count; i++)
		{
			var workExperience = workExperiences[i];
			var path = $"workExperiences[{i}]";
			if (IsEmptyWorkExperience(workExperience))
			{
				Add(results, "Remove empty work experience rows or complete organization, designation, and start date.", $"{path}.mediumStageName");
				continue;
			}

			Required(results, workExperience.MediumStageName, $"{path}.mediumStageName", "Organization is required.");
			Max(results, workExperience.MediumStageName, 256, $"{path}.mediumStageName", "Organization");
			Required(results, workExperience.Designation, $"{path}.designation", "Designation is required.");
			Max(results, workExperience.Designation, 256, $"{path}.designation", "Designation");
			Max(results, workExperience.Department, 256, $"{path}.department", "Department");
			Max(results, workExperience.City, 128, $"{path}.city", "City");
			EmployeeApplicationDateRules.ValidateWorkExperience(
				workExperience.StartDate,
				workExperience.EndDate,
				currentDate,
				path,
				results);
			ValidateLocation(results, workExperience.Country, $"{path}.country", "Country", required: false, CountryIsoCodeMaxLength);
			ValidateLocation(results, workExperience.State, $"{path}.state", "State", required: false, StateIsoCodeMaxLength);
		}
	}

	private static void ValidateLocation(
		List<ValidationResult> results,
		EmployeeApplicationLocationInfo? location,
		string path,
		string label,
		bool required,
		int isoCodeMaxLength)
	{
		if (location == null)
		{
			if (required)
			{
				Add(results, $"{label} is required.", path);
			}

			return;
		}

		if (required || !IsBlank(location.Name) || !IsBlank(location.IsoCode))
		{
			Required(results, location.Name, $"{path}.name", $"{label} name is required.");
			Required(results, location.IsoCode, $"{path}.isoCode", $"{label} ISO code is required.");
		}

		Max(results, location.Name, 128, $"{path}.name", $"{label} name");
		Max(results, location.IsoCode, isoCodeMaxLength, $"{path}.isoCode", $"{label} ISO code");
	}

	private static void ValidateIndianMobile(
		List<ValidationResult> results,
		string? value,
		string path,
		bool required = false)
	{
		if (IsBlank(value))
		{
			if (required)
			{
				Add(results, "Mobile number is required.", path);
			}

			return;
		}

		if (!IndianMobileRegex.IsMatch(value!.Trim()))
		{
			Add(results, "Mobile number must be 10 digits starting with 6-9.", path);
		}
	}

	private static void ValidatePincode(List<ValidationResult> results, string? value, string path)
	{
		Max(results, value, 10, path, "Pincode");
		if (!IsBlank(value) && !PincodeRegex.IsMatch(value!.Trim()))
		{
			Add(results, "Pincode must be 6 digits.", path);
		}
	}

	private static void RequiredEnum(
		List<ValidationResult> results,
		int? value,
		int min,
		int max,
		string path,
		string requiredMessage)
	{
		if (!value.HasValue)
		{
			Add(results, requiredMessage, path);
			return;
		}

		OptionalEnum(results, value, min, max, path, path);
	}

	private static void OptionalEnum(
		List<ValidationResult> results,
		int? value,
		int min,
		int max,
		string path,
		string label)
	{
		if (value.HasValue && (value.Value < min || value.Value > max))
		{
			Add(results, $"{label} has an invalid value.", path);
		}
	}

	private static void Required(
		List<ValidationResult> results,
		string? value,
		string path,
		string message)
	{
		if (IsBlank(value))
		{
			Add(results, message, path);
		}
	}

	private static void Max(
		List<ValidationResult> results,
		string? value,
		int maxLength,
		string path,
		string label)
	{
		if (value != null && value.Trim().Length > maxLength)
		{
			Add(results, $"{label} must be {maxLength} characters or less.", path);
		}
	}

	private static bool IsEmptyWorkExperience(EmployeeApplicationWorkExperienceDto input) =>
		IsBlank(input.MediumStageName) &&
		IsBlank(input.Designation) &&
		IsBlank(input.Department) &&
		IsBlank(input.City) &&
		input.StartDate == null &&
		input.EndDate == null &&
		IsEmptyLocation(input.Country) &&
		IsEmptyLocation(input.State);

	private static bool IsEmptyFamilyMember(EmployeeApplicationFamilyMemberDto input) =>
		IsBlank(input.Name) &&
		IsBlank(input.Occupation) &&
		IsBlank(input.MobileNo) &&
		IsBlank(input.Address1) &&
		IsBlank(input.Address2) &&
		IsBlank(input.Address3) &&
		IsBlank(input.City) &&
		IsBlank(input.Pincode) &&
		IsEmptyLocation(input.Country) &&
		IsEmptyLocation(input.State);

	private static bool IsEmptyLocation(EmployeeApplicationLocationInfo? location) =>
		location == null || (IsBlank(location.Name) && IsBlank(location.IsoCode));

	private static string NormalizeDigits(string? value) =>
		value == null ? string.Empty : new string(value.Where(char.IsDigit).ToArray());

	public static DateOnly GetInstitutionDate(DateTime value) =>
		EmployeeApplicationInstitutionClock.GetDate(value);

	private static int CalculateAge(DateOnly dateOfBirth, DateOnly referenceDate)
	{
		var age = referenceDate.Year - dateOfBirth.Year;
		if (referenceDate < dateOfBirth.AddYears(age))
		{
			age--;
		}

		return age;
	}

	private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);

	private static void Add(List<ValidationResult> results, string message, string memberName) =>
		results.Add(new ValidationResult(message, [memberName]));
}
