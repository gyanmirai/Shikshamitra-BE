using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public class EmployeePersonalInfoUserAccountCreator(
	IdentityUserManager userManager,
	IIdentityUserRepository userRepository,
	ILookupNormalizer lookupNormalizer,
	IGuidGenerator guidGenerator,
	ICurrentTenant currentTenant,
	ILogger<EmployeePersonalInfoUserAccountCreator> logger) : ITransientDependency
{
	public async Task<EmployeePersonalInfoUserAccount> CreateAsync(CreateEmployeePersonalInfoDto input)
	{
		var normalizedEmail = lookupNormalizer.NormalizeEmail(input.Email);
		var existingUserByEmail = await userRepository.FindByNormalizedEmailAsync(normalizedEmail);
		if (existingUserByEmail != null)
		{
			throw new BusinessException(HRErrorCodes.EmployeeEmailAlreadyExists);
		}

		var username = input.Email!;
		var password = BuildInitialPassword(username, input.MobileNo);

		var user = new IdentityUser(guidGenerator.Create(), username, input.Email, currentTenant.Id);
		user.Name = input.FirstName;
		user.Surname = input.LastName;
		user.SetEmailConfirmed(true);
		user.SetPhoneNumber(input.MobileNo, false);

		var userCreateResult = await userManager.CreateAsync(user, password);
		if (!userCreateResult.Succeeded)
		{
			var errors = string.Join(", ", userCreateResult.Errors.Select(e => e.Description));
			logger.LogError("User creation failed for {Email}: {Errors}", input.Email, errors);
			throw new BusinessException(HRErrorCodes.EmployeeUserCreationFailed).WithData("details", errors);
		}

		return new EmployeePersonalInfoUserAccount(user, username, password);
	}

	private static string BuildInitialPassword(string username, string mobileNo)
	{
		var last4Digits = mobileNo[^4..];
		var capitalizedUsername = char.ToUpper(username[0]) + username[1..];
		return $"{capitalizedUsername}@{last4Digits}";
	}

	public async Task DeleteAfterEmployeeCreationFailureAsync(IdentityUser user, string email)
	{
		var deleteResult = await userManager.DeleteAsync(user);
		if (deleteResult.Succeeded)
		{
			return;
		}

		var deleteErrors = string.Join(", ", deleteResult.Errors.Select(e => e.Description));
		logger.LogWarning(
			"Employee creation failed and cleanup user delete also failed for {Email}: {Errors}",
			email,
			deleteErrors);
	}
}

public sealed record EmployeePersonalInfoUserAccount(
	IdentityUser User,
	string Username,
	string Password);
