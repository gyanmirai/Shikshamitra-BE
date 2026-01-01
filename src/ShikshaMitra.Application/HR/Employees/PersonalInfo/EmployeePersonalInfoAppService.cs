using ShikshaMitra.Common;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

/// <summary>
/// Application service for managing employee personal information.
/// </summary>
[RemoteService(false)]
public class EmployeePersonalInfoAppService(
	IEmployeeRepository employeeRepository,
	EmployeeManager employeeManager)
	: ApplicationService, IEmployeePersonalInfoAppService
{
	public async Task<EmployeePersonalInfoDto> CreateAsync(CreateEmployeePersonalInfoDto input)
	{
		// Create employee using EmployeeManager (handles code generation, user creation, role assignment)
		var employee = await employeeManager.CreateEmployeeAsync(
			roleId: input.RoleId,
			firstName: input.FirstName,
			lastName: input.LastName,
			middleName: input.MiddleName,
			shortName: input.ShortName,
			employeeStatusType: input.EmployeeStatusType,
			dateOfBirth: input.DateOfBirth,
			familyCast: input.FamilyCaste, // DTO uses correct spelling, entity uses FamilyCast
			castType: input.CastType,
			genderType: input.GenderType,
			religion: input.Religion,
			maritalStatus: input.MaritalStatus,
			bloodGroup: input.BloodGroup,
			mobileNo: input.MobileNo,
			secondaryMobileNo: input.SecondaryMobileNo,
			email: input.Email,
			address1: input.Address1,
			address2: input.Address2,
			address3: input.Address3,
			department: input.Department,
			designation: input.Designation,
			dateOfJoining: input.DateOfJoining,
			employeeGroup: input.EmployeeGroup,
			qualification: input.Qualification,
			identificationMark1: input.IdentificationMark1,
			city: input.City,
			state: input.State,
			country: input.Country,
			pinCode: input.Pincode,
			shiftId: input.ShiftId
		);

		// Save the employee
		employee = await employeeRepository.InsertAsync(employee);

		// Return the mapped DTO
		return MapToDto(employee);
	}

	public async Task<EmployeePersonalInfoDto> GetAsync(Guid id)
	{
		// Repository.GetAsync throws EntityNotFoundException if not found
		var employee = await employeeRepository.GetAsync(id);
		return MapToDto(employee);
	}

	public async Task<EmployeePersonalInfoDto> UpdateAsync(Guid id, UpdateEmployeePersonalInfoDto input)
	{
		// Update employee using EmployeeManager
		var employee = await employeeManager.UpdateEmployeeAsync(
			employeeId: id,
			firstName: input.FirstName,
			lastName: input.LastName,
			middleName: input.MiddleName,
			shortName: input.ShortName,
			employeeStatusType: input.EmployeeStatusType,
			dateOfBirth: input.DateOfBirth,
			familyCast: input.FamilyCaste, // DTO uses correct spelling, entity uses FamilyCast
			castType: input.CastType,
			genderType: input.GenderType,
			religion: input.Religion,
			maritalStatus: input.MaritalStatus,
			bloodGroup: input.BloodGroup,
			mobileNo: input.MobileNo,
			secondaryMobileNo: input.SecondaryMobileNo,
			email: input.Email,
			address1: input.Address1,
			address2: input.Address2,
			address3: input.Address3,
			department: input.Department,
			designation: input.Designation,
			dateOfJoining: input.DateOfJoining,
			employeeGroup: input.EmployeeGroup,
			qualification: input.Qualification,
			roleId: input.RoleId,
			identificationMark1: input.IdentificationMark1,
			city: input.City,
			state: input.State,
			country: input.Country,
			pinCode: input.Pincode,
			shiftId: input.ShiftId
		);

		// Save the updated employee
		employee = await employeeRepository.UpdateAsync(employee);

		// Return the mapped DTO
		return MapToDto(employee);
	}

	private EmployeePersonalInfoDto MapToDto(Employee employee)
	{
		return new EmployeePersonalInfoDto
		{
			Id = employee.Id,
			EmployeeCode = employee.EmployeeCode,
			FullName = employee.EmployeeName,
			FirstName = employee.FirstName ?? string.Empty,
			LastName = employee.LastName ?? string.Empty,
			MiddleName = employee.MiddleName,
			ShortName = employee.ShortName,
			EmployeeStatusType = employee.EmployeeStatusType ?? EmployeeStatusType.Active,
			DateOfBirth = employee.DateOfBirth,
			FamilyCaste = employee.FamilyCast, // Entity uses FamilyCast
			CastType = employee.CastType,
			GenderType = employee.GenderType,
			Religion = employee.Religion,
			MaritalStatus = employee.MaritalStatus,
			BloodGroup = employee.BloodGroup,
			MobileNo = employee.MobileNo ?? string.Empty,
			SecondaryMobileNo = employee.SecondaryMobileNo,
			Email = employee.Email,
			Department = employee.Department,
			Designation = employee.Designation,
			DateOfJoining = employee.DateOfJoining,
			EmployeeGroup = employee.EmployeeGroup,
			Qualification = employee.Qualification,
			RoleId = employee.RoleId,
			RoleName = employee.Role?.Name,
			IdentificationMark1 = employee.IdentificationMark1,
			Address1 = employee.Address1,
			Address2 = employee.Address2,
			Address3 = employee.Address3,
			Country = employee.Country,
			State = employee.State,
			City = employee.City,
			Pincode = employee.PinCode,
			ShiftId = employee.ShiftId,
			Shift = null // Navigation property not included in API contract
		};
	}
}
