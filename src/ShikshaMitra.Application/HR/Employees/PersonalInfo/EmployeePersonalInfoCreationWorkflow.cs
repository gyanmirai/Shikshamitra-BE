using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.TenantSettings;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public class EmployeePersonalInfoCreationWorkflow(
	IEmployeeRepository employeeRepository,
	EmployeeManager employeeManager,
	EmployeeIdentityUniquenessChecker identityUniquenessChecker,
	EmployeePersonalInfoUserAccountCreator userAccountCreator,
	EmployeePersonalInfoProjection personalInfoProjection,
	EmployeePersonalInfoPostCommitEffects postCommitEffects,
	EmployeePersonalInfoPolicyOverrideWriter policyOverrideWriter,
	EmployeeCreationContactGuard contactGuard,
	ShiftReferenceValidator shiftReferenceValidator,
	ICurrentTenant currentTenant,
	IUnitOfWorkManager unitOfWorkManager,
	ILogger<EmployeePersonalInfoCreationWorkflow> logger) : ITransientDependency
{
	private const int MaxEmployeeCodeInsertAttempts = 5;
	private static readonly ConcurrentDictionary<string, SemaphoreSlim> CreationLocks = new();

	public async Task<EmployeePersonalInfoDto> CreateAsync(
		CreateEmployeePersonalInfoDto input,
		string? profilePictureFileName,
		byte[]? profilePictureContent)
	{
		return await CreateAsync(
			input,
			profilePictureFileName,
			profilePictureContent,
			useCurrentUnitOfWork: false);
	}

	public async Task<EmployeePersonalInfoDto> CreateInCurrentUnitOfWorkAsync(
		CreateEmployeePersonalInfoDto input,
		string? profilePictureFileName,
		byte[]? profilePictureContent)
	{
		if (unitOfWorkManager.Current is null)
		{
			throw new InvalidOperationException(
				"Atomic employee creation requires an active unit of work.");
		}

		return await CreateAsync(
			input,
			profilePictureFileName,
			profilePictureContent,
			useCurrentUnitOfWork: true);
	}

	private async Task<EmployeePersonalInfoDto> CreateAsync(
		CreateEmployeePersonalInfoDto input,
		string? profilePictureFileName,
		byte[]? profilePictureContent,
		bool useCurrentUnitOfWork)
	{
		var contact = contactGuard.RequireContactDetails(input.Email, input.MobileNo);
		input.Email = contact.Email;
		input.MobileNo = contact.MobileNo;
		input.PanNo = EmployeeIdentityUniquenessChecker.NormalizePan(input.PanNo)!;
		await identityUniquenessChecker.EnsurePanAndAdharAreUniqueAsync(input.PanNo, input.AdharNo);
		await shiftReferenceValidator.EnsureExistsAsync(input.ShiftId);

		Employee employee;
		EmployeePersonalInfoUserAccount? userAccount = null;
		var creationLock = CreationLocks.GetOrAdd(BuildCreationLockKey(), _ => new SemaphoreSlim(1, 1));
		await creationLock.WaitAsync();
		try
		{
			userAccount = await userAccountCreator.CreateAsync(input);
			employee = useCurrentUnitOfWork
				? await CreateAndInsertEmployeeAsync(userAccount.User.Id, input)
				: await CreateAndInsertEmployeeWithRetryAsync(userAccount.User.Id, input);
		}
		catch
		{
			if (userAccount != null)
			{
				await userAccountCreator.DeleteAfterEmployeeCreationFailureAsync(userAccount.User, contact.Email);
			}
			throw;
		}
		finally
		{
			creationLock.Release();
		}

		if (useCurrentUnitOfWork)
		{
			var employeeDto = await personalInfoProjection.ToPersonalInfoDtoAsync(
				await employeeRepository.GetAsync(employee.Id));
			postCommitEffects.Register(
				employee,
				employeeDto,
				input,
				userAccount,
				profilePictureFileName,
				profilePictureContent);
			return employeeDto;
		}

		await postCommitEffects.ExecuteAsync(
			employee,
			input,
			userAccount,
			profilePictureFileName,
			profilePictureContent);

		return await personalInfoProjection.ToPersonalInfoDtoAsync(await employeeRepository.GetAsync(employee.Id));
	}

	private string BuildCreationLockKey()
	{
		var tenantKey = currentTenant.Id?.ToString("N") ?? "host";
		return tenantKey;
	}

	private async Task<Employee> CreateAndInsertEmployeeWithRetryAsync(
		Guid userId,
		CreateEmployeePersonalInfoDto input)
	{
		for (var attempt = 1; attempt <= MaxEmployeeCodeInsertAttempts; attempt++)
		{
			try
			{
				using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true });
				var employee = await CreateAndInsertEmployeeAsync(userId, input);
				await uow.CompleteAsync();
				return employee;
			}
			catch (Exception ex) when (IsEmployeeCodeUniqueConstraintViolation(ex) && attempt < MaxEmployeeCodeInsertAttempts)
			{
				logger.LogWarning(
					ex,
					"Employee code collision detected for {Email}. Retrying create flow (attempt {Attempt}/{MaxAttempts}).",
					input.Email,
					attempt + 1,
					MaxEmployeeCodeInsertAttempts);
			}
		}

		throw new InvalidOperationException("Employee creation retry loop exited unexpectedly.");
	}

	private async Task<Employee> CreateAndInsertEmployeeAsync(
		Guid userId,
		CreateEmployeePersonalInfoDto input)
	{
		var employee = await employeeManager.CreateEmployeeAsync(
			userId: userId, roleId: input.RoleId,
			firstName: input.FirstName, lastName: input.LastName,
			middleName: input.MiddleName, shortName: input.ShortName!,
			employeeStatusType: input.EmployeeStatusType, dateOfBirth: input.DateOfBirth,
			familyCast: input.FamilyCaste, castType: input.CastType,
			genderType: input.GenderType, religion: input.Religion,
			maritalStatus: input.MaritalStatus, bloodGroup: input.BloodGroup,
			mobileNo: input.MobileNo, secondaryMobileNo: input.SecondaryMobileNo,
			email: input.Email, panNo: input.PanNo, adharNo: input.AdharNo,
			address1: input.Address1, address2: input.Address2,
			address3: input.Address3, department: input.Department,
			designation: input.Designation, dateOfJoining: input.DateOfJoining,
			employeeGroup: input.EmployeeGroup, qualification: input.Qualification,
			identificationMark1: input.IdentificationMark1, city: input.City,
			state: input.State, country: input.Country, pinCode: input.Pincode,
			shiftId: input.ShiftId);

		employee = await employeeRepository.InsertAsync(employee, autoSave: true);
		if (input.PolicyOverride != null)
		{
			await policyOverrideWriter.ApplyAsync(employee, input.PolicyOverride);
		}

		return employee;
	}

	private static bool IsEmployeeCodeUniqueConstraintViolation(Exception exception)
	{
		for (var current = exception; current != null; current = current.InnerException)
		{
			var message = current.Message;
			if (
				message.Contains("IX_Employees_TenantId_EmployeeCode_Unique", StringComparison.OrdinalIgnoreCase) ||
				message.Contains("AppEmployees.TenantId, AppEmployees.EmployeeCode", StringComparison.OrdinalIgnoreCase) ||
				message.Contains("duplicate key value violates unique constraint", StringComparison.OrdinalIgnoreCase) ||
				message.Contains("Cannot insert duplicate key row", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

}
