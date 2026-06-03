using ShikshaMitra.Common;
using System;

namespace ShikshaMitra.HR.Employees;

internal static class EmployeeFactory
{
	public static Employee CreateForUser(
		Guid? tenantId,
		Guid userId,
		Guid roleId,
		string employeeCode,
		string firstName,
		string lastName,
		string? middleName,
		string shortName,
		EmployeeStatusType employeeStatusType,
		DateOnly? dateOfBirth,
		string? familyCast,
		CastType? castType,
		GenderType? genderType,
		string? religion,
		MaritalStatusType? maritalStatus,
		string? bloodGroup,
		string? mobileNo,
		string? secondaryMobileNo,
		string? email,
		string? panNo,
		string? adharNo,
		string? address1,
		string? address2,
		string? address3,
		string? department,
		string? designation,
		DateOnly? dateOfJoining,
		EmployeeGroupType? employeeGroup,
		string? qualification,
		string? identificationMark1,
		string? city,
		State? state,
		Country? country,
		string? pinCode,
		Guid? shiftId)
	{
		var basicInfo = new EmployeeBasicInfo(
			ProfilePictureBlobName: null,
			LastName: lastName,
			FirstName: firstName,
			MiddleName: middleName,
			ShortName: shortName,
			EmployeeStatusType: employeeStatusType,
			DateOfBirth: dateOfBirth,
			FamilyCast: familyCast,
			CastType: castType,
			GenderType: genderType,
			Religion: religion,
			MaritalStatus: maritalStatus,
			BloodGroup: bloodGroup,
			MobileNo: mobileNo,
			SecondaryMobileNo: secondaryMobileNo,
			Email: email,
			PanNo: panNo ?? string.Empty,
			AdharNo: adharNo ?? string.Empty,
			Address1: address1,
			Address2: address2,
			Address3: address3,
			Department: department,
			Designation: designation,
			DateOfJoining: dateOfJoining,
			EmployeeGroup: employeeGroup,
			Qualification: qualification,
			RoleId: roleId,
			IdentificationMark1: identificationMark1,
			City: city,
			State: state,
			Country: country,
			PinCode: pinCode,
			ShiftId: shiftId);

		return new Employee(
			id: userId,
			tenantId: tenantId,
			userId: userId,
			employeeCode: employeeCode,
			basicInfo: basicInfo,
			bankDetails: new BankDetails(
				bankName: null,
				branchName: null,
				accountHolderName: null,
				accountNumber: null,
				ifscCode: null
			),
			workExperiences: null
		);
	}
}
