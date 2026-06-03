using System;
using ShikshaMitra.Common;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public static partial class PersonalInfoContractMapper
{
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
			PanNo = dto.PanNo,
			AdharNo = dto.AdharNo,
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
}
