using ShikshaMitra.Common;
using System;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.Employees;

public partial class EmployeeManager
{
	public async Task<Employee> CreateEmployeeAsync(
		Guid userId,
		Guid roleId,
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
		string? identificationMark1 = null,
		string? city = null,
		State? state = null,
		Country? country = null,
		string? pinCode = null,
		Guid? shiftId = null)
	{
		return await _creationManager.CreateEmployeeAsync(
			userId,
			roleId,
			firstName,
			lastName,
			middleName,
			shortName,
			employeeStatusType,
			dateOfBirth,
			familyCast,
			castType,
			genderType,
			religion,
			maritalStatus,
			bloodGroup,
			mobileNo,
			secondaryMobileNo,
			email,
			panNo,
			adharNo,
			address1,
			address2,
			address3,
			department,
			designation,
			dateOfJoining,
			employeeGroup,
			qualification,
			identificationMark1,
			city,
			state,
			country,
			pinCode,
			shiftId);
	}
}
