using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Account.Emailing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Portal;

// Keep the existing Account HTTP contract and owner-delivered reset-link flow.
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IAccountAppService), typeof(AccountAppService), typeof(StudentPortalAccountAppService))]
[RemoteService(false)]
public class StudentPortalAccountAppService(
	IdentityUserManager userManager,
	IIdentityRoleRepository roleRepository,
	IAccountEmailer accountEmailer,
	IdentitySecurityLogManager securityLogManager,
	IOptions<IdentityOptions> identityOptions,
	IRepository<StudentPortalAccountLink, Guid> links)
	: AccountAppService(userManager, roleRepository, accountEmailer, securityLogManager, identityOptions)
{
	[AllowAnonymous]
	[UnitOfWork(true)]
	public override async Task ResetPasswordAsync(ResetPasswordDto input)
	{
		var user = await UserManager.FindByIdAsync(input.UserId.ToString());
		var firstPortalSetup = CurrentTenant.Id.HasValue && user != null && user.TenantId == CurrentTenant.Id &&
			user.IsActive && !user.EmailConfirmed && string.IsNullOrEmpty(user.PasswordHash) &&
			await links.AnyAsync(link => link.UserId == user.Id);

		// ABP validates the protected, expiring reset token and password policy first.
		// A failed or replayed token never confirms a mailbox or grants access.
		await base.ResetPasswordAsync(input);
		if (!firstPortalSetup) return;
		var roles = await UserManager.GetRolesAsync(user!);
		if (roles.Count != 1 || roles[0] is not ("Student" or "Parent")) return;
		user!.SetEmailConfirmed(true);
		var result = await UserManager.UpdateAsync(user);
		if (!result.Succeeded)
			throw new BusinessException(StudentOnboardingErrorCodes.PortalAccountInvalid)
				.WithData("Reason", "Portal mailbox verification could not be completed. Request a new owner setup link.");
	}
}
