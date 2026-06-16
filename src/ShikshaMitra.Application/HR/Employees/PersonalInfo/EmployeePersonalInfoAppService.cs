using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR;
using ShikshaMitra.TenantSettings;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

/// <summary>
/// Single owner of employee lifecycle: create, read, update basic info.
/// </summary>
[RemoteService(false)]
[Authorize]
public class EmployeePersonalInfoAppService(
	IEmployeeRepository employeeRepository,
	IEmployeeFamilyMemberRepository familyMemberRepository,
	EmployeeManager employeeManager,
	EmployeePersonalInfoCreationWorkflow creationWorkflow,
	EmployeeOrganizationAssignmentSyncer organizationAssignmentSyncer,
	EmployeeIdentityUniquenessChecker identityUniquenessChecker,
	EmployeePersonalInfoProjection personalInfoProjection,
	ShiftReferenceValidator shiftReferenceValidator
)
	: ApplicationService, IEmployeePersonalInfoAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task<EmployeePersonalInfoDto> CreateAsync(CreateEmployeePersonalInfoDto input)
	{
		return await creationWorkflow.CreateAsync(input, null, null);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task<EmployeePersonalInfoDto> CreateWithProfilePictureAsync(
		CreateEmployeePersonalInfoDto input,
		string profilePictureFileName,
		byte[] profilePictureContent)
	{
		return await creationWorkflow.CreateAsync(input, profilePictureFileName, profilePictureContent);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	public async Task<EmployeePersonalInfoDto> GetAsync(Guid id)
	{
		var employee = await employeeRepository.GetAsync(id);
		return await personalInfoProjection.ToPersonalInfoDtoAsync(employee);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit)]
	public async Task<EmployeePersonalInfoDto> UpdateAsync(Guid id, UpdateEmployeePersonalInfoDto input)
	{
		var existingEmployee = await employeeRepository.GetAsync(id);
		EnsureEmailUnchanged(existingEmployee.Email, input.Email);
		input.PanNo = EmployeeIdentityUniquenessChecker.NormalizePan(input.PanNo);
		await identityUniquenessChecker.EnsurePanAndAdharAreUniqueAsync(input.PanNo, input.AdharNo, id);
		await shiftReferenceValidator.EnsureExistsAsync(input.ShiftId);

		var employee = await employeeManager.UpdateEmployeeAsync(
			employeeId: id,
			firstName: input.FirstName!,
			lastName: input.LastName!,
			middleName: input.MiddleName,
			shortName: input.ShortName!,
			employeeStatusType: existingEmployee.EmployeeStatusType ?? EmployeeStatusType.Active,
			dateOfBirth: input.DateOfBirth,
			familyCast: input.FamilyCaste, // DTO uses correct spelling, entity uses FamilyCast
			castType: input.CastType,
			genderType: input.GenderType,
			religion: input.Religion,
			maritalStatus: input.MaritalStatus,
			bloodGroup: input.BloodGroup,
			mobileNo: input.MobileNo,
			secondaryMobileNo: input.SecondaryMobileNo,
			email: existingEmployee.Email,
			panNo: input.PanNo,
			adharNo: input.AdharNo,
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

		employee = await employeeRepository.UpdateAsync(employee);
		await organizationAssignmentSyncer.SyncAsync(employee.Id, input.OrganizationIds);

		return await personalInfoProjection.ToPersonalInfoDtoAsync(await employeeRepository.GetAsync(employee.Id));
	}

	private static void EnsureEmailUnchanged(string? existingEmail, string? requestedEmail)
	{
		if (string.Equals(existingEmail?.Trim(), requestedEmail?.Trim(), StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.EmployeeEmailCannotBeChanged)
			.WithData("field", nameof(UpdateEmployeePersonalInfoDto.Email));
	}

	public async Task<EmployeeDto> GetByUserIdAsync(Guid userId)
	{
		var employee = await employeeRepository.GetEmployeeByUserIdAsync(userId)
			?? throw new EntityNotFoundException(typeof(Employee), userId);

		return await personalInfoProjection.ToEmployeeDtoAsync(employee);
	}

	public async Task<EmployeeDto> GetWithDetailsAsync(Guid id)
	{
		var employee = await employeeRepository.GetWithDetailsAsync(id)
			?? throw new EntityNotFoundException(typeof(Employee), id);

		var employeeDto = await personalInfoProjection.ToEmployeeDtoAsync(employee);

		var familyMembers = await familyMemberRepository.GetByEmployeeIdAsync(id);
		personalInfoProjection.PopulateFamilyDetails(employeeDto, familyMembers);

		return employeeDto;
	}

	public async Task<EmployeeDto> UpdateBankDetailsAsync(Guid employeeId, BankDetailsDto input)
	{
		var bankDetails = ObjectMapper.Map<BankDetailsDto, BankDetails>(input);
		var employee = await employeeManager.UpdateBankDetailsByEmployeeIdAsync(employeeId, bankDetails);
		employee = await employeeRepository.UpdateAsync(employee);
		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

}
