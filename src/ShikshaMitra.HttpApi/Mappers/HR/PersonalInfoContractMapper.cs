using System;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public static partial class PersonalInfoContractMapper
{
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
			PanNo = NormalizePan(request.PanNo),
			AdharNo = request.AdharNo,
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
				LeavePolicyId = request.PolicyOverride.LeavePolicyId,
				EffectiveFrom = request.PolicyOverride.EffectiveFrom,
				EffectiveTo = request.PolicyOverride.EffectiveTo,
				AssignmentReason = request.PolicyOverride.AssignmentReason,
				ConfirmationToken = request.PolicyOverride.ConfirmationToken
			}
		};
	}

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
			PanNo = NormalizePan(request.PanNo),
			AdharNo = request.AdharNo,
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
}
