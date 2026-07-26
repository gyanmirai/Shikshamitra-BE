using System;
using System.Collections.Generic;
using ShikshaMitra.Common;

namespace ShikshaMitra.HR.Employees.Onboarding;

internal static class EmployeeApplicationFormMapper
{
	public static EmployeeApplicationLocationInfo? ToLocation(Country? value) =>
		value == null ? null : new EmployeeApplicationLocationInfo { Name = value.Name, IsoCode = value.IsoCode };

	public static EmployeeApplicationLocationInfo? ToLocation(State? value) =>
		value == null ? null : new EmployeeApplicationLocationInfo { Name = value.Name, IsoCode = value.IsoCode };

	private static EmployeeApplicationLocationInfo? ToLocation(EmployeeApplicationLocationSnapshot? value) =>
		value == null ? null : new EmployeeApplicationLocationInfo { Name = value.Name, IsoCode = value.IsoCode };

	public static EmployeeApplicationPersonalInfoDto? ToPersonalInfoDto(
		EmployeeApplicationPersonalInfoSnapshot? value) =>
		value == null
			? null
			: new EmployeeApplicationPersonalInfoDto
			{
				FirstName = value.FirstName,
				MiddleName = value.MiddleName,
				ShortName = value.ShortName,
				LastName = value.LastName,
				Email = value.Email,
				MobileNo = value.MobileNo,
				SecondaryMobileNo = value.SecondaryMobileNo,
				DateOfBirth = value.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
				Gender = value.Gender,
				Religion = value.Religion,
				CasteType = value.CasteType,
				FamilyCaste = value.FamilyCaste,
				MaritalStatus = value.MaritalStatus,
				BloodGroup = value.BloodGroup,
				IsTeaching = value.IsTeaching,
				Department = value.Department,
				Designation = value.Designation,
				Qualification = value.Qualification,
				IdentificationMark1 = value.IdentificationMark1,
				Address1 = value.Address1,
				Address2 = value.Address2,
				Address3 = value.Address3,
				Country = ToLocation(value.Country),
				State = ToLocation(value.State),
				City = value.City,
				Pincode = value.Pincode
			};

	public static EmployeeApplicationFamilyInfoDto? ToFamilyInfoDto(
		EmployeeApplicationFamilyInfoSnapshot? value) =>
		value == null
			? null
			: new EmployeeApplicationFamilyInfoDto
			{
				FatherDetails = ToFamilyMemberDto(value.FatherDetails),
				MotherDetails = ToFamilyMemberDto(value.MotherDetails),
				SpouseDetails = ToFamilyMemberDto(value.SpouseDetails)
			};

	public static EmployeeApplicationAdditionalInfoDto? ToAdditionalInfoDto(
		EmployeeApplicationAdditionalInfoSnapshot? value) =>
		value == null
			? null
			: new EmployeeApplicationAdditionalInfoDto
			{
				PersonWithDisability = value.PersonWithDisability,
				SecondLanguage = value.SecondLanguage,
				IsLibraryMember = value.IsLibraryMember,
				IsTransportMember = value.IsTransportMember
			};

	public static EmployeeApplicationWorkExperienceDto ToWorkExperienceDto(WorkExperience experience)
	{
		var (city, state, country) = ParseWorkExperienceAddress(experience.OrganizationAddress);
		return new EmployeeApplicationWorkExperienceDto
		{
			MediumStageName = experience.OrganizationName,
			Designation = experience.Designation,
			Department = experience.Department,
			City = city,
			State = state,
			Country = country,
			StartDate = experience.FromDate?.ToDateTime(TimeOnly.MinValue),
			EndDate = experience.ToDate?.ToDateTime(TimeOnly.MinValue)
		};
	}

	public static WorkExperience ToWorkExperience(EmployeeApplicationWorkExperienceDto input) =>
		new(
			input.MediumStageName,
			BuildAddress(input),
			input.StartDate.HasValue ? DateOnly.FromDateTime(input.StartDate.Value) : null,
			input.EndDate.HasValue ? DateOnly.FromDateTime(input.EndDate.Value) : null,
			input.Department,
			input.Designation,
			null,
			null);

	private static EmployeeApplicationFamilyMemberDto? ToFamilyMemberDto(
		EmployeeApplicationFamilyMemberSnapshot? value) =>
		value == null
			? null
			: new EmployeeApplicationFamilyMemberDto
			{
				Name = value.Name,
				Occupation = value.Occupation,
				MobileNo = value.MobileNo,
				Address1 = value.Address1,
				Address2 = value.Address2,
				Address3 = value.Address3,
				Country = ToLocation(value.Country),
				State = ToLocation(value.State),
				City = value.City,
				Pincode = value.Pincode
			};

	private static string? BuildAddress(EmployeeApplicationWorkExperienceDto input)
	{
		var parts = new List<string>();
		if (!string.IsNullOrWhiteSpace(input.City))
		{
			parts.Add(input.City);
		}
		if (!string.IsNullOrWhiteSpace(input.State?.Name))
		{
			parts.Add(FormatLocation(input.State));
		}
		if (!string.IsNullOrWhiteSpace(input.Country?.Name))
		{
			parts.Add(FormatLocation(input.Country));
		}
		return parts.Count == 0 ? null : string.Join(", ", parts);
	}

	private static string FormatLocation(EmployeeApplicationLocationInfo location) =>
		string.IsNullOrWhiteSpace(location.IsoCode)
			? location.Name
			: $"{location.Name} ({location.IsoCode})";

	private static (string? City, EmployeeApplicationLocationInfo? State, EmployeeApplicationLocationInfo? Country)
		ParseWorkExperienceAddress(string? address)
	{
		if (string.IsNullOrWhiteSpace(address))
		{
			return (null, null, null);
		}
		var parts = address.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		return (
			parts.Length > 0 ? parts[0] : null,
			parts.Length > 1 ? ParseLocationPart(parts[1]) : null,
			parts.Length > 2 ? ParseLocationPart(parts[2]) : null);
	}

	private static EmployeeApplicationLocationInfo? ParseLocationPart(string? part)
	{
		if (string.IsNullOrWhiteSpace(part))
		{
			return null;
		}
		var openParen = part.LastIndexOf('(');
		var closeParen = part.LastIndexOf(')');
		return openParen > 0 && closeParen > openParen
			? new EmployeeApplicationLocationInfo
			{
				Name = part[..openParen].Trim(),
				IsoCode = part[(openParen + 1)..closeParen].Trim()
			}
			: new EmployeeApplicationLocationInfo
			{
				Name = part.Trim(),
				IsoCode = string.Empty
			};
	}
}
