using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Employees;

[RemoteService(false)]
public class EmployeeBasicInfoWorkflow(
	IEmployeeRepository employeeRepository,
	EmployeeManager employeeManager,
	ILogger<EmployeeBasicInfoWorkflow> logger,
	EmployeeIdentifierUniquenessService identifierUniquenessService,
	EmployeeCreationContactGuard contactGuard,
	EmployeeIdentityCreationWorkflow identityCreationWorkflow,
	IEmployeeEmailService emailService) : ApplicationService, ITransientDependency
{
	public async Task<EmployeeDto> CreateAsync(CreateEmployeeBasicInfoDto input)
	{
		logger.LogInformation(
			"Starting employee creation process for: {Email}, Name: {FirstName} {LastName}, Role: {RoleId}",
			input.Email,
			input.FirstName,
			input.LastName,
			input.RoleId);

		var contact = contactGuard.RequireContactDetails(input.Email, input.MobileNo);
		input.Email = contact.Email;
		input.MobileNo = contact.MobileNo;
		input.PanNo = EmployeeIdentifierNormalizer.NormalizePan(input.PanNo);
		input.AdharNo = EmployeeIdentifierNormalizer.NormalizeAdhar(input.AdharNo);
		await identifierUniquenessService.EnsurePanAndAdharAreUniqueAsync(input.PanNo, input.AdharNo);

		using var uow = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true });
		try
		{
			var identity = await identityCreationWorkflow.CreateAsync(input);
			logger.LogInformation(
				"Creating employee record: UserId={UserId}, RoleId={RoleId}, Department={Department}, Designation={Designation}",
				identity.UserId,
				input.RoleId,
				input.Department,
				input.Designation);

			var employee = await employeeManager.CreateEmployeeAsync(
				userId: identity.UserId,
				roleId: input.RoleId,
				firstName: input.FirstName!,
				lastName: input.LastName!,
				middleName: input.MiddleName,
				shortName: input.ShortName!,
				employeeStatusType: input.EmployeeStatusType,
				dateOfBirth: input.DateOfBirth,
				familyCast: input.FamilyCast,
				castType: input.CastType,
				genderType: input.GenderType,
				religion: input.Religion,
				maritalStatus: input.MaritalStatus,
				bloodGroup: input.BloodGroup,
				mobileNo: input.MobileNo,
				secondaryMobileNo: input.SecondaryMobileNo,
				email: input.Email,
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
				identificationMark1: input.IdentificationMark1,
				city: input.City,
				state: input.State,
				country: input.Country,
				pinCode: input.PinCode,
				shiftId: input.ShiftId);

			employee = await employeeRepository.InsertAsync(employee);

			logger.LogInformation(
				"Employee record created successfully: EmployeeId={EmployeeId}, EmployeeCode={EmployeeCode}, UserId={UserId}",
				employee.Id,
				employee.EmployeeCode,
				identity.UserId);

			await uow.CompleteAsync();

			logger.LogInformation(
				"Transaction completed successfully for employee creation: EmployeeId={EmployeeId}, EmployeeCode={EmployeeCode}, Email={Email}",
				employee.Id,
				employee.EmployeeCode,
				input.Email);

			var fullName = $"{input.FirstName} {input.LastName}".Trim();
			await emailService.SendWelcomeEmailAsync(input.Email, fullName, identity.Username, identity.Password);
			logger.LogInformation("Welcome email queued for employee: {Email}", input.Email);

			return ObjectMapper.Map<Employee, EmployeeDto>(employee);
		}
		catch (BusinessException ex)
		{
			logger.LogWarning(
				ex,
				"Employee creation validation failed for email: {Email}. Message: {Message}",
				input.Email,
				ex.Message);
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Unexpected error during employee creation for email: {Email}, Name: {FirstName} {LastName}",
				input.Email,
				input.FirstName,
				input.LastName);
			throw;
		}
	}

	public async Task<EmployeeDto> UpdateAsync(Guid employeeId, UpdateEmployeeBasicInfoDto input)
	{
		input.PanNo = EmployeeIdentifierNormalizer.NormalizePan(input.PanNo);
		input.AdharNo = EmployeeIdentifierNormalizer.NormalizeAdhar(input.AdharNo);
		await identifierUniquenessService.EnsurePanAndAdharAreUniqueAsync(input.PanNo, input.AdharNo, employeeId);

		var employee = await employeeManager.UpdateEmployeeAsync(
			employeeId,
			firstName: input.FirstName!,
			lastName: input.LastName!,
			middleName: input.MiddleName,
			shortName: input.ShortName!,
			employeeStatusType: input.EmployeeStatusType,
			dateOfBirth: input.DateOfBirth,
			familyCast: input.FamilyCast,
			castType: input.CastType,
			genderType: input.GenderType,
			religion: input.Religion,
			maritalStatus: input.MaritalStatus,
			bloodGroup: input.BloodGroup,
			mobileNo: input.MobileNo,
			secondaryMobileNo: input.SecondaryMobileNo,
			email: input.Email,
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
			pinCode: input.PinCode,
			shiftId: input.ShiftId);

		employee = await employeeRepository.UpdateAsync(employee);
		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}
}
