using System;

namespace ShikshaMitra.HR.Employees.Onboarding;

internal static class EmployeeApplicationSnapshotMapper
{
	public static EmployeeApplicationPersonalInfoSnapshot ToPersonalInfo(
		EmployeeApplicationPersonalInfoDto input) =>
		new(
			input.FirstName,
			input.MiddleName,
			input.ShortName,
			input.LastName,
			input.Email,
			input.MobileNo,
			input.SecondaryMobileNo,
			input.DateOfBirth.HasValue ? DateOnly.FromDateTime(input.DateOfBirth.Value) : null,
			input.Gender,
			input.Religion,
			input.CasteType,
			input.FamilyCaste,
			input.MaritalStatus,
			input.BloodGroup,
			input.IsTeaching,
			input.Department,
			input.Designation,
			input.Qualification,
			input.IdentificationMark1,
			input.Address1,
			input.Address2,
			input.Address3,
			ToLocation(input.Country),
			ToLocation(input.State),
			input.City,
			input.Pincode);

	public static EmployeeApplicationFamilyInfoSnapshot? ToFamilyInfo(
		EmployeeApplicationFamilyInfoDto? input)
	{
		if (input == null)
		{
			return null;
		}

		var father = ToFamilyMember(input.FatherDetails);
		var mother = ToFamilyMember(input.MotherDetails);
		var spouse = ToFamilyMember(input.SpouseDetails);
		return father == null && mother == null && spouse == null
			? null
			: new EmployeeApplicationFamilyInfoSnapshot(father, mother, spouse);
	}

	public static EmployeeApplicationAdditionalInfoSnapshot? ToAdditionalInfo(
		EmployeeApplicationAdditionalInfoDto? input) =>
		input == null || !HasAdditionalInfo(input)
			? null
			: new EmployeeApplicationAdditionalInfoSnapshot(
				input.PersonWithDisability,
				input.SecondLanguage,
				input.IsLibraryMember,
				input.IsTransportMember);

	private static EmployeeApplicationFamilyMemberSnapshot? ToFamilyMember(
		EmployeeApplicationFamilyMemberDto? input) =>
		input == null || IsEmptyFamilyMember(input)
			? null
			: new EmployeeApplicationFamilyMemberSnapshot(
				input.Name,
				input.Occupation,
				input.MobileNo,
				input.Address1,
				input.Address2,
				input.Address3,
				ToLocation(input.Country),
				ToLocation(input.State),
				input.City,
				input.Pincode);

	private static EmployeeApplicationLocationSnapshot? ToLocation(
		EmployeeApplicationLocationInfo? input) =>
		input == null || string.IsNullOrWhiteSpace(input.Name)
			? null
			: new EmployeeApplicationLocationSnapshot(input.Name, input.IsoCode);

	private static bool HasAdditionalInfo(EmployeeApplicationAdditionalInfoDto input) =>
		input.PersonWithDisability ||
		!string.IsNullOrWhiteSpace(input.SecondLanguage) ||
		input.IsLibraryMember ||
		input.IsTransportMember;

	private static bool IsEmptyFamilyMember(EmployeeApplicationFamilyMemberDto input) =>
		string.IsNullOrWhiteSpace(input.Name) &&
		string.IsNullOrWhiteSpace(input.Occupation) &&
		string.IsNullOrWhiteSpace(input.MobileNo) &&
		string.IsNullOrWhiteSpace(input.Address1) &&
		string.IsNullOrWhiteSpace(input.Address2) &&
		string.IsNullOrWhiteSpace(input.Address3) &&
		(input.Country == null || string.IsNullOrWhiteSpace(input.Country.Name)) &&
		(input.State == null || string.IsNullOrWhiteSpace(input.State.Name)) &&
		string.IsNullOrWhiteSpace(input.City) &&
		string.IsNullOrWhiteSpace(input.Pincode);
}
