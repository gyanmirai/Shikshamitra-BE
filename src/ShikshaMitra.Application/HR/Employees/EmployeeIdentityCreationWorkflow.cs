using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace ShikshaMitra.HR.Employees;

[RemoteService(false)]
public class EmployeeIdentityCreationWorkflow(
	IGuidGenerator guidGenerator,
	ILogger<EmployeeIdentityCreationWorkflow> logger,
	IdentityUserManager userManager,
	IIdentityUserRepository userRepository,
	ILookupNormalizer lookupNormalizer) : ApplicationService, ITransientDependency
{
	public async Task<EmployeeIdentityCreationResult> CreateAsync(CreateEmployeeBasicInfoDto input)
	{
		var normalizedEmail = lookupNormalizer.NormalizeEmail(input.Email!);
		var existingUserByEmail = await userRepository.FindByNormalizedEmailAsync(normalizedEmail);
		if (existingUserByEmail != null)
		{
			logger.LogWarning(
				"Staff creation failed: Email '{Email}' is already in use by user {UserId}",
				input.Email,
				existingUserByEmail.Id);
			throw new BusinessException(HRErrorCodes.EmployeeEmailAlreadyExists)
				.WithData("email", input.Email!);
		}

		var username = input.Email!;
		logger.LogInformation(
			"Using email as username '{Username}' for staff identity account",
			username);

		var password = BuildInitialPassword(username, input.MobileNo!);
		var userId = guidGenerator.Create();
		var user = new IdentityUser(
			userId,
			username,
			input.Email,
			CurrentTenant.Id);

		user.Name = input.FirstName;
		user.Surname = input.LastName;
		user.SetEmailConfirmed(true);
		user.SetPhoneNumber(input.MobileNo, false);

		logger.LogInformation(
			"Attempting to create user: UserId={UserId}, Username={Username}, Email={Email}, TenantId={TenantId}",
			userId,
			username,
			input.Email,
			CurrentTenant.Id);

		var userCreateResult = await userManager.CreateAsync(user, password);
		if (!userCreateResult.Succeeded)
		{
			var errors = string.Join(", ", userCreateResult.Errors.Select(e => e.Description));
			logger.LogError(
				"User creation failed for email: {Email}, Username: {Username}. Errors: {Errors}",
				input.Email,
				username,
				errors);
			throw new BusinessException(HRErrorCodes.EmployeeUserCreationFailed)
				.WithData("details", errors);
		}

		logger.LogInformation(
			"User created successfully: UserId={UserId}, Username={Username}, Email={Email}",
			userId,
			username,
			input.Email);

		var createdUser = await userRepository.FindAsync(user.Id);
		if (createdUser == null)
		{
			logger.LogError(
				"User verification failed: User with ID {UserId} was not found after creation",
				userId);
			throw new BusinessException(HRErrorCodes.EmployeeUserVerificationFailed);
		}

		logger.LogInformation("User verified successfully: UserId={UserId}", userId);
		return new EmployeeIdentityCreationResult(createdUser.Id, username, password);
	}

	private static string BuildInitialPassword(string username, string mobileNo)
	{
		var last4Digits = mobileNo.Substring(mobileNo.Length - 4);
		var capitalizedUsername = char.ToUpper(username[0]) + username.Substring(1);
		return $"{capitalizedUsername}@{last4Digits}";
	}
}

public sealed record EmployeeIdentityCreationResult(Guid UserId, string Username, string Password);
