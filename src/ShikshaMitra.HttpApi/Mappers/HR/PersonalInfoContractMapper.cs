using System;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

/// <summary>
/// Maps between API contract DTOs and internal application DTOs
/// </summary>
public static class PersonalInfoContractMapper
{
	/// <summary>
	/// Converts API request to internal create DTO
	/// </summary>
	public static CreateEmployeePersonalInfoDto ToCreateDto(PersonalInfoRequest request)
	{
		return new CreateEmployeePersonalInfoDto
		{
			FirstName = request.FirstName,
			MiddleName = request.MiddleName,
			LastName = request.LastName,
			ShortName = request.ShortName,
			DateOfBirth = request.Dob.HasValue ? DateOnly.FromDateTime(request.Dob.Value) : null,
			GenderType = request.Gender.HasValue ? MapGender(request.Gender.Value) : null,
			Religion = request.Religion,
			CastType = request.CasteType.HasValue ? MapCasteType(request.CasteType.Value) : null,
			FamilyCaste = request.FamilyCaste,
			MaritalStatus = request.MaritalStatus.HasValue ? MapMaritalStatus(request.MaritalStatus.Value) : null,
			BloodGroup = request.BloodGroup.HasValue ? MapBloodGroup(request.BloodGroup.Value) : null,
			Email = request.Email,
			MobileNo = request.MobileNo,
			SecondaryMobileNo = request.SecondaryMobileNo,
			DateOfJoining = DateOnly.FromDateTime(request.Doj),
			Department = request.Department?.ToString(),
			RoleId = request.RoleId,
			ShiftId = request.ShiftId,
			OrganizationIds = request.OrganizationIds,
			Address1 = request.Address1,
			Address2 = request.Address2,
			Address3 = request.Address3,
			Country = request.Country != null ? new Country(request.Country.Name, request.Country.IsoCode) : null,
			State = request.State != null ? new State(request.State.Name, request.State.IsoCode) : null,
			City = request.City,
			Pincode = request.PinCode,
			EmployeeGroup = request.IsTeaching ? EmployeeGroupType.Teaching : EmployeeGroupType.NonTeaching,
			PolicyOverride = request.PolicyOverride == null ? null : new CreateEmployeePolicyOverrideDto
			{
				AcademicYearId = request.PolicyOverride.AcademicYearId,
				AttendancePolicyId = request.PolicyOverride.AttendancePolicyId,
				PayrollPolicyId = request.PolicyOverride.PayrollPolicyId,
				EffectiveFrom = request.PolicyOverride.EffectiveFrom,
				EffectiveTo = request.PolicyOverride.EffectiveTo,
				AssignmentReason = request.PolicyOverride.AssignmentReason,
				ConfirmationToken = request.PolicyOverride.ConfirmationToken
			}
		};
	}

	/// <summary>
	/// Converts API request to internal update DTO
	/// </summary>
	public static UpdateEmployeePersonalInfoDto ToUpdateDto(PersonalInfoRequest request)
	{
		return new UpdateEmployeePersonalInfoDto
		{
			FirstName = request.FirstName,
			MiddleName = request.MiddleName,
			LastName = request.LastName,
			ShortName = request.ShortName,
			DateOfBirth = request.Dob.HasValue ? DateOnly.FromDateTime(request.Dob.Value) : null,
			GenderType = request.Gender.HasValue ? MapGender(request.Gender.Value) : null,
			Religion = request.Religion,
			CastType = request.CasteType.HasValue ? MapCasteType(request.CasteType.Value) : null,
			FamilyCaste = request.FamilyCaste,
			MaritalStatus = request.MaritalStatus.HasValue ? MapMaritalStatus(request.MaritalStatus.Value) : null,
			BloodGroup = request.BloodGroup.HasValue ? MapBloodGroup(request.BloodGroup.Value) : null,
			Email = request.Email,
			MobileNo = request.MobileNo,
			SecondaryMobileNo = request.SecondaryMobileNo,
			DateOfJoining = DateOnly.FromDateTime(request.Doj),
			Department = request.Department?.ToString(),
			RoleId = request.RoleId,
			ShiftId = request.ShiftId,
			OrganizationIds = request.OrganizationIds,
			Address1 = request.Address1,
			Address2 = request.Address2,
			Address3 = request.Address3,
			Country = request.Country != null ? new Country(request.Country.Name, request.Country.IsoCode) : null,
			State = request.State != null ? new State(request.State.Name, request.State.IsoCode) : null,
			City = request.City,
			Pincode = request.PinCode,
			EmployeeGroup = request.IsTeaching ? EmployeeGroupType.Teaching : EmployeeGroupType.NonTeaching
		};
	}

	/// <summary>
	/// Converts internal DTO to API response
	/// </summary>
	public static PersonalInfoResponse ToResponse(EmployeePersonalInfoDto dto)
	{
		return new PersonalInfoResponse
		{
			Id = dto.Id,
			EmployeeCode = dto.EmployeeCode,
			FullName = dto.FullName,
			UserId = dto.UserId,
			HasProfilePicture = !string.IsNullOrEmpty(dto.ProfilePictureBlobName),
			FirstName = dto.FirstName,
			MiddleName = dto.MiddleName,
			LastName = dto.LastName,
			ShortName = dto.ShortName,
			Dob = dto.DateOfBirth.HasValue ? dto.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue) : null,
			Gender = dto.GenderType.HasValue ? MapGenderToInt(dto.GenderType.Value) : null,
			Religion = dto.Religion,
			CasteType = dto.CastType.HasValue ? MapCasteTypeToInt(dto.CastType.Value) : null,
			FamilyCaste = dto.FamilyCaste,
			MaritalStatus = dto.MaritalStatus.HasValue ? MapMaritalStatusToInt(dto.MaritalStatus.Value) : null,
			BloodGroup = !string.IsNullOrEmpty(dto.BloodGroup) ? MapBloodGroupToInt(dto.BloodGroup) : null,
			Email = dto.Email,
			MobileNo = dto.MobileNo,
			SecondaryMobileNo = dto.SecondaryMobileNo,
			Doj = dto.DateOfJoining.HasValue ? dto.DateOfJoining.Value.ToDateTime(TimeOnly.MinValue) : DateTime.MinValue,
			IsTeaching = dto.EmployeeGroup == EmployeeGroupType.Teaching,
			Department = dto.Department,
			RoleId = dto.RoleId,
			ShiftId = dto.ShiftId ?? Guid.Empty,
			OrganizationIds = dto.OrganizationIds,
			Organizations = dto.Organizations,
			OrganizationNames = dto.OrganizationNames,
			Address1 = dto.Address1,
			Address2 = dto.Address2,
			Address3 = dto.Address3,
			Country = dto.Country != null ? new CountryInfo { Name = dto.Country.Name, IsoCode = dto.Country.IsoCode } : null,
			State = dto.State != null ? new StateInfo { Name = dto.State.Name, IsoCode = dto.State.IsoCode } : null,
			City = dto.City,
			PinCode = dto.Pincode
		};
	}

	#region Gender Mapping

	private static GenderType MapGender(int gender)
	{
		return gender switch
		{
			1 => GenderType.Male,
			2 => GenderType.Female,
			_ => throw new BusinessException(HRErrorCodes.InvalidGenderValue)
		};
	}

	private static int MapGenderToInt(GenderType gender)
	{
		return gender switch
		{
			GenderType.Male => 1,
			GenderType.Female => 2,
			_ => throw new BusinessException(HRErrorCodes.InvalidGenderValue)
		};
	}

	#endregion

	#region CasteType Mapping

	private static CastType MapCasteType(int casteType)
	{
		return casteType switch
		{
			0 => CastType.General,
			1 => CastType.OBC,
			2 => CastType.SC,
			3 => CastType.ST,
			_ => throw new BusinessException(HRErrorCodes.InvalidCasteTypeValue)
		};
	}

	private static int MapCasteTypeToInt(CastType casteType)
	{
		return casteType switch
		{
			CastType.General => 0,
			CastType.OBC => 1,
			CastType.SC => 2,
			CastType.ST => 3,
			_ => 0
		};
	}

	#endregion

	#region MaritalStatus Mapping

	private static MaritalStatusType MapMaritalStatus(int maritalStatus)
	{
		return maritalStatus switch
		{
			0 => MaritalStatusType.Unmarried,
			1 => MaritalStatusType.Married,
			2 => MaritalStatusType.Divorced,
			3 => MaritalStatusType.Widowed,
			_ => throw new BusinessException(HRErrorCodes.InvalidMaritalStatusValue)
		};
	}

	private static int MapMaritalStatusToInt(MaritalStatusType maritalStatus)
	{
		return maritalStatus switch
		{
			MaritalStatusType.Unmarried => 0,
			MaritalStatusType.Married => 1,
			MaritalStatusType.Divorced => 2,
			MaritalStatusType.Widowed => 3,
			_ => 0
		};
	}

	#endregion

	#region BloodGroup Mapping

	private static string MapBloodGroup(int bloodGroup)
	{
		return bloodGroup switch
		{
			1 => "A+",
			2 => "A-",
			3 => "B+",
			4 => "B-",
			5 => "AB+",
			6 => "AB-",
			7 => "O+",
			8 => "O-",
			_ => throw new BusinessException(HRErrorCodes.InvalidBloodGroupValue)
		};
	}

	private static int MapBloodGroupToInt(string bloodGroup)
	{
		return bloodGroup?.ToUpperInvariant() switch
		{
			"A+" => 1,
			"A-" => 2,
			"B+" => 3,
			"B-" => 4,
			"AB+" => 5,
			"AB-" => 6,
			"O+" => 7,
			"O-" => 8,
			_ => throw new BusinessException(HRErrorCodes.InvalidBloodGroupValue)
		};
	}

	#endregion
}
