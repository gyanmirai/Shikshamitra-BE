using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.Employees;

public class EmployeeAppService(
	IEmployeeRepository employeeRepository,
	EmployeeManager employeeManager,
	IIdentityUserRepository userRepository,
	IIdentityRoleRepository roleRepository) :
	CrudAppService<Employee, EmployeeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEmployeeDto>(
		employeeRepository),
	IEmployeeService
{
	public async Task<EmployeeDto?> GetEmployeeByUserId(Guid userId)
	{
		Employee? employee = await employeeRepository.GetEmployeeByUserIdAsync(userId);

		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	public async Task<EmployeeDto> CreateBasicInfoAsync(CreateEmployeeBasicInfoDto input)
	{
		// Use EmployeeManager to create the employee with domain logic
		Employee employee = await employeeManager.CreateEmployeeAsync(
			userId: input.UserId,
			roleId: input.RoleId,
			employeeCode: input.EmployeeCode,
			firstName: input.FirstName,
			lastName: input.LastName,
			middleName: input.MiddleName,
			shortName: input.ShortName,
			employeeStatusType: input.EmployeeStatusType,
			dateOfBirth: input.DateOfBirth,
			profilePicture: input.ProfilePicture,
			familyCast: input.FamilyCast,
			castType: input.CastType,
			gender: input.Gender,
			religion: input.Religion,
			maritalStatus: input.MaritalStatus,
			bloodGroup: input.BloodGroup,
			mobileNo: input.MobileNo,
			email: input.Email,
			department: input.Department,
			designation: input.Designation,
			dateOfJoining: input.DateOfJoining,
			employeeGroup: input.EmployeeGroup,
			qualification: input.Qualification,
			identificationMark1: input.IdentificationMark1,
			identificationMark2: input.IdentificationMark2,
			identificationMark3: input.IdentificationMark3,
			address: input.Address
		);

		// Save the employee
		employee = await employeeRepository.InsertAsync(employee);

		// Return the DTO
		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	public async Task<EmployeeDto> UpdateFamilyInfo(Guid employeeId, UpdateEmployeeFamilyDetailsDto input)
	{
		// convert the input to domain model and update the employee

		var fatherDetails = ObjectMapper.Map<ContactDetailsDto, ContactDetails>(input.FatherDetails);
		var motherDetails = ObjectMapper.Map<ContactDetailsDto, ContactDetails>(input.MotherDetails);
		var spouseDetails = ObjectMapper.Map<ContactDetailsDto, ContactDetails>(input.SpouseDetails);

		var employee =
			await employeeManager.UpdateFamilyInfoByEmployeeIdAsync(employeeId, fatherDetails, motherDetails,
				spouseDetails);

		// Save the employee
		employee = await employeeRepository.UpdateAsync(employee);

		// Return the DTO
		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	public async Task<EmployeeDto> UpdateExperienceInfo(Guid employeeId, List<WorkExperienceDto> input)
	{
		// convert the input to domain model and update the employee

		List<WorkExperience> workExperiences = ObjectMapper.Map<List<WorkExperienceDto>, List<WorkExperience>>(input);

		Employee employee =
			await employeeManager.UpdateExperienceInfoByEmployeeIdAsync(employeeId, workExperiences);

		// Save the employee
		employee = await employeeRepository.UpdateAsync(employee);

		// Return the DTO
		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	public async Task<EmployeeDto> UpdateBankDetails(Guid employeeId, BankDetailsDto input)
	{
		// convert the input to domain model and update the employee

		BankDetails bankDetails = ObjectMapper.Map<BankDetailsDto, BankDetails>(input);

		Employee employee =
			await employeeManager.UpdateBankDetailsByEmployeeIdAsync(employeeId, bankDetails);

		// Save the employee
		employee = await employeeRepository.UpdateAsync(employee);

		// Return the DTO
		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}
	
}