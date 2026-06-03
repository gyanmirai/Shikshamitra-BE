using System;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Employees;

public class EmployeeUpdateManager(
	EmployeeLookupReader employeeLookupReader,
	EmployeeUserRoleManager userRoleManager
) : DomainService
{
	public async Task<Employee> UpdateEmployeeAsync(
		Guid employeeId,
		string firstName,
		string lastName,
		string? middleName,
		string shortName,
		EmployeeStatusType employeeStatusType,
		DateOnly? dateOfBirth,
		string? familyCast = null,
		CastType? castType = null,
		GenderType? genderType = null,
		string? religion = null,
		MaritalStatusType? maritalStatus = null,
		string? bloodGroup = null,
		string? mobileNo = null,
		string? secondaryMobileNo = null,
		string? email = null,
		string? panNo = null,
		string? adharNo = null,
		string? address1 = null,
		string? address2 = null,
		string? address3 = null,
		string? department = null,
		string? designation = null,
		DateOnly? dateOfJoining = null,
		EmployeeGroupType? employeeGroup = null,
		string? qualification = null,
		Guid? roleId = null,
		string? identificationMark1 = null,
		string? city = null,
		State? state = null,
		Country? country = null,
		string? pinCode = null,
		Guid? shiftId = null
	)
	{
		Employee employee = await employeeLookupReader.GetRequiredAsync(employeeId);

		if (roleId.HasValue && employee.RoleId != roleId.Value && employee.UserId != null)
		{
			await userRoleManager.SwapRoleAsync(employee.UserId.Value, employee.RoleId, roleId.Value);
		}

		employee.UpdateBasicInfo(new EmployeeBasicInfo(
			ProfilePictureBlobName: employee.ProfilePictureBlobName,
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
			PanNo: panNo,
			AdharNo: adharNo,
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
			ShiftId: shiftId));
		EmployeeRuleValidator.Validate(employee, DateOnly.FromDateTime(Clock.Now));

		return employee;
	}
}
