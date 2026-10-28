using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Encryption;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

/// <summary>Claim and credential preparation commit before transport; delivery never runs in account creation.</summary>
public class StudentCredentialEmailDispatcher(IRepository<StudentCredentialEmailRequest, Guid> requests,
	IStudentCredentialEmailEligibilityResolver eligibility, IdentityUserManager users, IEmailSender sender,
	IStringEncryptionService encryption, IUnitOfWorkManager units, ICurrentTenant tenant,
	IUserClaimsPrincipalFactory<IdentityUser> principals, ICurrentPrincipalAccessor currentPrincipal,
	IPermissionChecker permissions, IStudentOnboardingAppService onboarding,
	IStudentCredentialRecipientVerifier? recipients = null, StudentSiblingParentService? siblingParents = null) : ITransientDependency
{
	public async Task DispatchAsync(Guid id)
	{
		// Optimistic concurrency on the durable claim prevents two workers preparing/sending the same intent.
		using (var prepare = units.Begin(requiresNew: true, isTransactional: true))
		{
			var row = await requests.FindAsync(id);
			if (row == null || !tenant.Id.HasValue || row.TenantId != tenant.Id) return;
			if (row.IsExistingParentHelp() && (siblingParents == null || !await siblingParents.IsReusedAsync(row.CaseId, row.AccountType)))
			{
				row.Cancel("The reviewed existing Parent link is no longer available."); await requests.UpdateAsync(row, false); await prepare.CompleteAsync(); return;
			}
			if (siblingParents != null && (!row.IsExistingParentHelp() && await siblingParents.IsReusedAsync(row.CaseId, row.AccountType) ||
				!row.RequiresSavedOwnerValidation() && await siblingParents.IsManualParentCaseAsync(row.CaseId, row.AccountType)))
			{
				if (row.Status is "Pending" or "Processing" or "Failed")
				{
					row.Cancel("Parent login uses the existing account or requires a manual first-time request.");
					await requests.UpdateAsync(row, false);
				}
				await prepare.CompleteAsync(); return;
			}
			if (row.Status == "Processing")
			{
				row.RecoverInterrupted(DateTime.UtcNow);
				await requests.UpdateAsync(row, false);
				await prepare.CompleteAsync(); return;
			}
			if (!row.IsDue(DateTime.UtcNow)) return;
			if (!await ActorMaySendAsync(row))
			{
				row.Cancel("The requesting staff member no longer has permission to send this email.");
				await requests.UpdateAsync(row, false); await prepare.CompleteAsync(); return;
			}
			var state = await ResolveDeliveryAsync(row);
			if (!await SavedRecipientMatchesAsync(row, state))
			{
				row.Cancel("The email no longer matches the saved account-owner record. Review the Student record before requesting credentials.");
				await requests.UpdateAsync(row, false); await prepare.CompleteAsync(); return;
			}
			if (state.WaitingForCompletion && MatchesRecipient(row, state))
			{
				// Preserve the explicit intent without preparing a password or starving
				// ready rows in the bounded worker batch while other accounts are pending.
				row.DeferUntilCompletion(DateTime.UtcNow);
				await requests.UpdateAsync(row, false); await prepare.CompleteAsync(); return;
			}
			if (!Matches(row, state))
			{
				row.Cancel("Account access, recipient, school settings or onboarding readiness changed. Email cancelled.");
				await requests.UpdateAsync(row, false); await prepare.CompleteAsync(); return;
			}
			var user = await users.FindByIdAsync(row.UserId.ToString());
			if (user == null || !user.IsActive || user.TenantId != tenant.Id)
			{
				row.Cancel("The account is unavailable or inactive."); await requests.UpdateAsync(row, false);
				await prepare.CompleteAsync(); return;
			}
			row.Claim(DateTime.UtcNow);
			if (!row.IsExistingParentHelp() && !await users.HasPasswordAsync(user))
			{
				// The user's password is never generated from email/mobile or exposed through staff APIs.
				var password = GenerateInitialPassword();
				var result = await users.AddPasswordAsync(user, password);
				if (!result.Succeeded)
				{
					row.Failed(DateTime.UtcNow); await requests.UpdateAsync(row, false);
					await prepare.CompleteAsync(); return;
				}
				user.SetShouldChangePasswordOnNextLogin(true);
				var updated = await users.UpdateAsync(user);
				if (!updated.Succeeded) throw new InvalidOperationException("Initial account password policy could not be applied.");
				row.SetInitialPassword(encryption.Encrypt(password)!);
			}
			await requests.UpdateAsync(row, false); await prepare.CompleteAsync();
		}
		using var delivery = units.Begin(requiresNew: true, isTransactional: true);
		var request = await requests.GetAsync(id);
		if (request.Status != "Processing") return;
		try
		{
			// Re-read the relationship immediately before sending, including exits and guardian replacements.
			var state = await ResolveDeliveryAsync(request);
			if (siblingParents != null && !request.IsExistingParentHelp() && await siblingParents.IsReusedAsync(request.CaseId, request.AccountType) ||
				!Matches(request, state) || !await SavedRecipientMatchesAsync(request, state) || !await ActorMaySendAsync(request)) request.Cancel("Account access, staff authorization or readiness changed. Email cancelled.");
			else
			{
				var user = await users.FindByIdAsync(request.UserId.ToString());
				if (user == null || !user.IsActive) request.Cancel("The account is unavailable or inactive.");
				else
				{
					var password = request.IsExistingParentHelp() || request.EncryptedInitialPassword == null ? null : encryption.Decrypt(request.EncryptedInitialPassword);
					if (password != null && (!user.ShouldChangePasswordOnNextLogin || !await users.CheckPasswordAsync(user, password)))
					{
						// The owner/admin may have changed the password while delivery was failing.
						// Never resend a stale password or replace the owner's chosen credentials.
						request.ClearInitialPassword(); password = null;
					}
					// A sent email retry uses owner recovery. Existing sibling credentials are never overwritten.
					string? resetUrl = null;
					if (request.IsExistingParentHelp() || password != null || request.SentAt.HasValue || user.ShouldChangePasswordOnNextLogin)
					{
						var token = await users.GeneratePasswordResetTokenAsync(user);
						var baseUri = new Uri(state.LoginUrl, UriKind.Absolute);
						resetUrl = new Uri(baseUri, "/account/reset-password").AbsoluteUri + "?userId=" + user.Id +
							"&resetToken=" + Uri.EscapeDataString(token) + "&tenantId=" + tenant.Id;
					}
					await sender.SendAsync(request.Email, password == null ? "Student account access - " + state.SchoolName : "Your school app credentials - " + state.SchoolName,
						StudentCredentialEmailTemplate.Build(state, user, password, resetUrl, user.ShouldChangePasswordOnNextLogin), true);
					request.Sent(DateTime.UtcNow);
				}
			}
		}
		catch (Exception)
		{
			// Neither transport exceptions nor rendered messages are logged: they can contain credentials.
			request.Failed(DateTime.UtcNow);
		}
		await requests.UpdateAsync(request, false); await delivery.CompleteAsync();
	}
	private static string GenerateInitialPassword()
	{
		const string characters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
		string password;
		do
		{
			password = RandomNumberGenerator.GetString(characters, 10);
		}
		while (!password.Any(char.IsUpper) || !password.Any(char.IsLower) ||
			!password.Any(char.IsDigit) || password.All(char.IsLetterOrDigit));
		return password;
	}
	private async Task<bool> ActorMaySendAsync(StudentCredentialEmailRequest row)
	{
		if (!row.RequestedByUserId.HasValue) return false;
		var actor = await users.FindByIdAsync(row.RequestedByUserId.Value.ToString());
		if (actor == null || !actor.IsActive || actor.TenantId != tenant.Id) return false;
		using var principal = currentPrincipal.Change(await principals.CreateAsync(actor));
		if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)) return false;
		try
		{
			// Revoking a staff member's academic scope also revokes their queued send authority.
			await onboarding.EnsureCaseAccessAsync(row.CaseId);
			if (row.IsExistingParentHelp() && (siblingParents == null || !await siblingParents.CanSendExistingParentHelpAsync(row.CaseId))) return false;
			return true;
		}
		catch (AbpAuthorizationException) { return false; }
		catch (EntityNotFoundException) { return false; }
	}
	private static bool Matches(StudentCredentialEmailRequest row, StudentCredentialEmailEligibility state) =>
		state.IsReady && MatchesRecipient(row, state);
	private static bool MatchesRecipient(StudentCredentialEmailRequest row, StudentCredentialEmailEligibility state) =>
		state.EmailEnabled && state.LinkId == row.LinkId && state.UserId == row.UserId &&
		state.StudentId == row.StudentId && string.Equals(state.Email.Trim(), row.Email.Trim(), StringComparison.OrdinalIgnoreCase);
	private Task<bool> SavedRecipientMatchesAsync(StudentCredentialEmailRequest row, StudentCredentialEmailEligibility state) =>
		!row.RequiresSavedOwnerValidation() ? Task.FromResult(true) : recipients == null ? Task.FromResult(false)
			: recipients.IsCurrentOwnerAsync(row.CaseId, row.AccountType, state.Relationship, row.Email);
	private Task<StudentCredentialEmailEligibility> ResolveDeliveryAsync(StudentCredentialEmailRequest row) =>
		row.RequiresSavedOwnerValidation() ? eligibility.ResolveExplicitAsync(row.CaseId, row.AccountType, false)
			: eligibility.ResolveAsync(row.CaseId, row.AccountType, false);
}
