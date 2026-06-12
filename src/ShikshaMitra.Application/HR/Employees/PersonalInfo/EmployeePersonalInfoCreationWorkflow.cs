using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public class EmployeePersonalInfoCreationWorkflow(
	IEmployeeRepository employeeRepository,
	EmployeeManager employeeManager,
	EmployeeDocumentManager employeeDocumentManager,
	IEmployeeEmailService emailService,
	IDistributedEventBus distributedEventBus,
	IHrPolicyAssignmentAppService policyAssignmentAppService,
	EmployeeOrganizationAssignmentSyncer organizationAssignmentSyncer,
	EmployeeIdentityUniquenessChecker identityUniquenessChecker,
	EmployeePersonalInfoUserAccountCreator userAccountCreator,
	EmployeePersonalInfoProjection personalInfoProjection,
	EmployeeCreationContactGuard contactGuard,
	ShiftReferenceValidator shiftReferenceValidator,
	ICurrentTenant currentTenant,
	IUnitOfWorkManager unitOfWorkManager,
	IClock clock,
	ILogger<EmployeePersonalInfoCreationWorkflow> logger) : ITransientDependency
{
	private const int MaxEmployeeCodeInsertAttempts = 5;
	private const string OnboardingDefaultPolicyConfirmationTokenMarker = "__employee_onboarding_default_policy_assignment__";

	public async Task<EmployeePersonalInfoDto> CreateAsync(
		CreateEmployeePersonalInfoDto input,
		string? profilePictureFileName,
		byte[]? profilePictureContent)
	{
		var contact = contactGuard.RequireContactDetails(input.Email, input.MobileNo);
		input.Email = contact.Email;
		input.MobileNo = contact.MobileNo;
		input.PanNo = EmployeeIdentityUniquenessChecker.NormalizePan(input.PanNo)!;
		await identityUniquenessChecker.EnsurePanAndAdharAreUniqueAsync(input.PanNo, input.AdharNo);
		await shiftReferenceValidator.EnsureExistsAsync(input.ShiftId);

		var userAccount = await userAccountCreator.CreateAsync(input);

		Employee employee;
		try
		{
			employee = await CreateAndInsertEmployeeWithRetryAsync(userAccount.User.Id, input);
		}
		catch
		{
			await userAccountCreator.DeleteAfterEmployeeCreationFailureAsync(userAccount.User, contact.Email);
			throw;
		}

		if (profilePictureContent != null && !string.IsNullOrWhiteSpace(profilePictureFileName))
		{
			await employeeDocumentManager.UploadProfilePictureAsync(
				employee.Id, profilePictureFileName, profilePictureContent);
		}

		logger.LogInformation(
			"Employee created: EmployeeId={EmployeeId}, Code={EmployeeCode}, Email={Email}",
			employee.Id, employee.EmployeeCode, input.Email);

		await distributedEventBus.PublishAsync(new HR.Events.EmployeeCreatedEto
		{
			EmployeeId = employee.Id,
			EmployeeCode = employee.EmployeeCode,
			FullName = $"{input.FirstName} {input.LastName}".Trim(),
			Email = input.Email,
			TenantId = currentTenant.Id,
			CreatedAt = clock.Now
		});

		var fullName = $"{input.FirstName} {input.LastName}".Trim();
		await emailService.SendWelcomeEmailAsync(input.Email, fullName, userAccount.Username, userAccount.Password);

		return await personalInfoProjection.ToPersonalInfoDtoAsync(await employeeRepository.GetAsync(employee.Id));
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
				await organizationAssignmentSyncer.SyncAsync(employee.Id, input.OrganizationIds);
				if (input.PolicyOverride != null)
				{
					await CreateDirectPolicyOverrideAsync(employee, input.PolicyOverride);
				}

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

	private async Task CreateDirectPolicyOverrideAsync(Employee employee, CreateEmployeePolicyOverrideDto input)
	{
		var overrideInput = new UpsertEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = input.AcademicYearId,
			AttendancePolicyId = input.AttendancePolicyId,
			PayrollPolicyId = input.PayrollPolicyId,
			LeavePolicyId = input.LeavePolicyId,
			EffectiveFrom = input.EffectiveFrom,
			EffectiveTo = input.EffectiveTo,
			AssignmentReason = input.AssignmentReason ?? "Employee creation override",
			ConfirmationToken = input.ConfirmationToken
		};

		await EnsureOnboardingDefaultPolicyOverrideConfirmedAsync(employee, overrideInput);
		await policyAssignmentAppService.UpsertEmployeeOverrideAsync(overrideInput);
	}

	private async Task EnsureOnboardingDefaultPolicyOverrideConfirmedAsync(
		Employee employee,
		UpsertEmployeePolicyOverrideDto input)
	{
		if (!string.Equals(
			    input.ConfirmationToken,
			    OnboardingDefaultPolicyConfirmationTokenMarker,
			    StringComparison.Ordinal))
		{
			return;
		}

		var targetKey = HrPolicyAssignment.NormalizeTargetKey(
			HrPolicyAssignmentTargetType.Employee,
			employee.Id.ToString("D"));
		var preview = await policyAssignmentAppService.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = "create",
			Assignment = new CreateUpdateHrPolicyAssignmentDto
			{
				AcademicYearId = input.AcademicYearId,
				TargetType = HrPolicyMapping.MapAssignmentTargetType(HrPolicyAssignmentTargetType.Employee),
				TargetKey = targetKey,
				TargetDisplayName = employee.EmployeeName,
				AttendancePolicyId = input.AttendancePolicyId,
				PayrollPolicyId = input.PayrollPolicyId,
				LeavePolicyId = input.LeavePolicyId,
				EffectiveFrom = input.EffectiveFrom.Date,
				EffectiveTo = input.EffectiveTo?.Date,
				IsActive = true,
				Priority = 100,
				AssignmentReason = input.AssignmentReason
			}
		});

		input.ConfirmationToken = preview.RequiresConfirmation
			? preview.ConfirmationToken
			: null;
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
