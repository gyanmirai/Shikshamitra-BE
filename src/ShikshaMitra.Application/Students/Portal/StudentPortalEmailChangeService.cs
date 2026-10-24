using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace ShikshaMitra.Students.Portal;

/// <summary>Owner-verified changes to an existing portal login, never account replacement or password reset.</summary>
[RemoteService(false)]
[ExposeServices(typeof(StudentPortalEmailChangeService), typeof(IStudentPortalEmailChangeAppService))]
public class StudentPortalEmailChangeService(IdentityUserManager users, IStudentRepository students,
	IRepository<StudentPortalAccountLink, Guid> links, IRepository<StudentOnboardingCase, Guid> cases,
	IRepository<StudentCredentialEmailRequest, Guid> credentialRequests,
	IRepository<StudentGuardianAccessReview, Guid> accessReviews, StudentScopeAccessService scope,
	IRepository<StudentOnboardingEvent, Guid> events, StudentJourneyAuditWriter audit,
	IBackgroundJobManager jobs, IEmailSender sender, IConfiguration configuration,
	IUserClaimsPrincipalFactory<IdentityUser> principals, ICurrentPrincipalAccessor principalAccessor) : ApplicationService, IStudentPortalEmailChangeAppService
{
	public async Task RequestAsync(StudentOnboardingCase item, StudentPortalAccountLink link, UpdateStudentCredentialsEmailDto input)
	{
		await CheckStaffAsync();
		if (!(await new UpdateStudentCredentialsEmailDtoValidator().ValidateAsync(input)).IsValid) throw Invalid("Enter a valid email and review the saved recipient.");
		await LazyServiceProvider.LazyGetRequiredService<IStudentOnboardingAppService>().EnsureCaseAccessAsync(item.Id);
		var savedCase = await cases.GetAsync(item.Id);
		if (savedCase.TenantId != CurrentTenant.Id || savedCase.StudentId != link.StudentId || savedCase.Status == StudentOnboardingStatus.Cancelled) throw Invalid();
		var user = await users.FindByIdAsync(link.UserId.ToString()) ?? throw Invalid();
		var allLinks = await CheckAccountAsync(user);
		var topology = await TopologyAsync(allLinks);
		if (input.EmailEditToken != EditToken(user, topology)) throw Invalid("The account or linked children changed. Refresh App Login before editing.");
		if (!allLinks.Any(x => x.Id == link.Id && x.StudentId == item.StudentId && x.Relationship == input.Relationship)) throw Invalid();
		var previous = StudentPortalEmailChangeState.Read(user);
		if (input.CancelPendingChange)
		{
			if (previous != null && previous.Status is "Pending" or "Sent" or "Processing")
			{
				previous.Status = "Cancelled"; previous.Save(user);
				if (!(await users.UpdateAsync(user)).Succeeded) throw Invalid();
				await events.InsertAsync(audit.CreateOnboardingEvent(item.Id, "PortalEmailChangeCancelled",
					"Staff cancelled the pending login email verification. The current login, password and child links were preserved."), false);
			}
			return;
		}
		if (allLinks.Count > 1 && !input.ConfirmSharedParent) throw Invalid("Confirm that the new login email applies to every child linked to this parent.");
		var email = input.Email.Trim();
		if (StudentPortalProfileEmail.Same(user.Email, email)) return;
		await CheckDuplicateAsync(user, email);
		if (previous != null && previous.Status is "Pending" or "Sent" && previous.RequestedAt.AddMinutes(2) > Clock.Now &&
			previous.IsCurrent(user, Clock.Now) && StudentPortalProfileEmail.Same(previous.Email, email)) return;
		if (previous != null && previous.RequestedAt.AddMinutes(2) > Clock.Now)
			throw Invalid("Wait two minutes before requesting another email change.");
		var state = new StudentPortalEmailChangeState
		{
			RequestId = GuidGenerator.Create(), CaseId = item.Id, UserId = user.Id, ActorId = CurrentUser.Id!.Value,
			Email = email, PreviousEmail = user.Email, PreviousUserName = user.UserName,
			SecurityStampHash = StudentPortalEmailChangeState.Fingerprint(user.SecurityStamp), Topology = topology,
			RequestedAt = Clock.Now, ExpiresAt = Clock.Now.AddHours(24)
		};
		state.Save(user);
		if (!(await users.UpdateAsync(user)).Succeeded) throw Invalid();
		await events.InsertAsync(audit.CreateOnboardingEvent(item.Id, "PortalEmailVerificationRequested",
			"Staff requested owner verification of a new login email. The current login, password and child links remain unchanged."), false);
		// ABP's existing persistent background-job store participates in this transaction.
		// A rollback never leaves an email job for an uncommitted account change.
		await jobs.EnqueueAsync(new StudentPortalEmailChangeJobArgs { TenantId = CurrentTenant.Id!.Value, UserId = user.Id, RequestId = state.RequestId });
	}

	public async Task EnrichAsync(IReadOnlyList<StudentCredentialsRecipientDto> recipients, Guid studentId, bool canEdit, bool canUpdate)
	{
		var ownedLinks = await links.GetListAsync(x => x.StudentId == studentId);
		var student = await students.GetCredentialsProfileAsync(studentId);
		foreach (var recipient in recipients)
		{
			if (recipient.Relationship == "guardian") continue; // Historical guardian repair retains its existing reviewed workflow.
			var link = ownedLinks.FirstOrDefault(x => x.AccountType == recipient.AccountType);
			recipient.HasLinkedAccount = link != null;
			recipient.CanEditEmail = canEdit && (link == null || canUpdate && link.Relationship == recipient.Relationship);
			if (link == null)
			{
				if (student != null) recipient.EmailEditToken = ProfileToken(student);
				continue;
			}
			var user = await users.FindByIdAsync(link.UserId.ToString());
			if (user == null || user.TenantId != CurrentTenant.Id) { recipient.CanEditEmail = false; continue; }
			var accountLinks = await links.GetListAsync(x => x.UserId == user.Id);
			var topology = await TopologyAsync(accountLinks);
			recipient.EmailEditToken = EditToken(user, topology);
			recipient.LinkedStudentCount = accountLinks.Select(x => x.StudentId).Distinct().Count();
			var state = StudentPortalEmailChangeState.Read(user);
			if (state != null && state.Status is not ("Verified" or "Cancelled") && link.Relationship == recipient.Relationship)
			{
				recipient.PendingEmail = state.Email;
				recipient.EmailChangeStatus = state.ExpiresAt <= Clock.Now ? "Expired"
					: !state.IsCurrent(user, Clock.Now) || state.Topology != topology ? "Stale" : state.Status;
			}
			if (!canUpdate) recipient.EmailEditBlockedReason = "An authorized account administrator must update this login email.";
			else if (link.Relationship != recipient.Relationship) recipient.EmailEditBlockedReason = "A different parent is linked to this login.";
		}
	}

	[AllowAnonymous]
	[DisableAuditing]
	[UnitOfWork(true)]
	public virtual async Task ConfirmAsync(ConfirmStudentPortalEmailDto input)
	{
		if (input.TenantId == Guid.Empty || input.UserId == Guid.Empty || input.RequestId == Guid.Empty ||
			string.IsNullOrWhiteSpace(input.Token) || input.Token.Length > 4096) throw Invalid("This verification link is invalid or expired. Ask your school to request a new one.");
		// Resolve the school through the normal tenant header/middleware, not a
		// body-controlled tenant switch that could bypass inactive-school guards.
		if (CurrentTenant.Id != input.TenantId) throw Invalid();
		var user = await users.FindByIdAsync(input.UserId.ToString()) ?? throw Invalid();
		if (user.TenantId != input.TenantId) throw Invalid();
		var state = StudentPortalEmailChangeState.Read(user);
		if (state == null || state.RequestId != input.RequestId) throw Invalid();
		if (state.Status == "Verified") return;
		if (state.Status is not ("Pending" or "Sent") || !state.IsCurrent(user, Clock.Now)) throw Invalid();
		using var actor = await UseActorAsync(state.ActorId);
		await CheckStaffAsync();
		var accountLinks = await CheckAccountAsync(user);
		await CheckRequestSourceAsync(state, accountLinks);
		if (await TopologyAsync(accountLinks) != state.Topology) throw Invalid("The linked profiles changed. Ask your school to review and request email verification again.");
		await CheckDuplicateAsync(user, state.Email);
		// Identity verifies the protected, expiring, user/security-stamp/new-email-bound token.
		// It changes only email verification data, not the password hash or any child link.
		if (!(await users.ChangeEmailAsync(user, state.Email, input.Token)).Succeeded) throw Invalid("This verification link is invalid or expired. Ask your school to request a new one.");
		if (StudentPortalProfileEmail.Same(state.PreviousUserName, state.PreviousEmail) &&
			!(await users.SetUserNameAsync(user, state.Email)).Succeeded) throw Invalid();
		foreach (var link in accountLinks)
		{
			var student = await students.GetCredentialsProfileAsync(link.StudentId, trackChanges: true) ?? throw Invalid();
			StudentPortalProfileEmail.Apply(student, link.Relationship, state.Email, state.ActorId);
			await students.UpdateAsync(student, false);
		}
		var studentIds = accountLinks.Select(x => x.StudentId).ToArray();
		foreach (var item in await cases.GetListAsync(x => Enumerable.Contains(studentIds, x.StudentId)))
		{
			StudentSiblingParentService.RefreshEmailSelection(item, user.Id, state.PreviousEmail, state.Email);
			await cases.UpdateAsync(item, false);
			await events.InsertAsync(audit.CreateOnboardingEvent(item.Id, "PortalEmailChanged",
				"The account owner verified a new login email. The existing password and student links were preserved."), false);
		}
		foreach (var request in await credentialRequests.GetListAsync(x => x.UserId == user.Id && (x.Status == "Pending" || x.Status == "Failed")))
		{
			request.Cancel("The owner verified a new login email. Staff may request credentials for the current saved email.");
			await credentialRequests.UpdateAsync(request, false);
		}
		state.Status = "Verified"; state.Save(user);
		if (!(await users.UpdateAsync(user)).Succeeded) throw Invalid();
		await LazyServiceProvider.LazyGetRequiredService<StudentPortalEmailSessionRevoker>().RevokeAsync(user.Id);
		await CurrentUnitOfWork!.SaveChangesAsync();
	}

	[DisableAuditing]
	[UnitOfWork(true)]
	public virtual async Task SendVerificationAsync(StudentPortalEmailChangeJobArgs args)
	{
		if (!CurrentTenant.Id.HasValue || CurrentTenant.Id != args.TenantId) throw Invalid();
		var user = await users.FindByIdAsync(args.UserId.ToString());
		if (user == null || user.TenantId != CurrentTenant.Id) return;
		var state = StudentPortalEmailChangeState.Read(user);
		if (state == null || state.RequestId != args.RequestId || state.Status != "Pending") return;
		using var actor = await UseActorAsync(state.ActorId);
		await CheckStaffAsync();
		var accountLinks = await CheckAccountAsync(user);
		await CheckRequestSourceAsync(state, accountLinks);
		if (!state.IsCurrent(user, Clock.Now) || await TopologyAsync(accountLinks) != state.Topology) throw Invalid();
		await CheckDuplicateAsync(user, state.Email);
		// Flush the optimistic claim before transport. Concurrent jobs fail before
		// SMTP; a transport failure rolls the claim back for the durable job retry.
		state.Status = "Processing"; state.Save(user);
		if (!(await users.UpdateAsync(user)).Succeeded) throw Invalid();
		await CurrentUnitOfWork!.SaveChangesAsync();
		var token = await users.GenerateChangeEmailTokenAsync(user, state.Email);
		var clientUrl = configuration["App:ClientUrl"] ?? configuration["App:AngularUrl"];
		if (!Uri.TryCreate(clientUrl, UriKind.Absolute, out var client) || client.Scheme is not ("http" or "https")) throw Invalid("The school's login address is not configured.");
		var url = new Uri(client, "/account/confirm-portal-email").AbsoluteUri + "?tenantId=" + args.TenantId +
			"&userId=" + user.Id + "&requestId=" + state.RequestId + "&token=" + Uri.EscapeDataString(token);
		await sender.SendAsync(state.Email, "Verify your school app login email",
			"<p>Your school requested a login email change.</p><p><a href=\"" + HtmlEncoder.Default.Encode(url) +
			"\">Confirm email change</a></p><p>Your existing password and linked children will be preserved. " +
			"Your old login email stays active until you confirm. If you did not request this, contact your school.</p>", true);
		state.Status = "Sent"; state.Save(user);
		if (!(await users.UpdateAsync(user)).Succeeded) throw Invalid();
		await CurrentUnitOfWork!.SaveChangesAsync();
	}

	private async Task CheckStaffAsync()
	{
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
		await AuthorizationService.CheckAsync(IdentityPermissions.Users.Update);
	}
	private async Task<IDisposable> UseActorAsync(Guid actorId)
	{
		var actor = await users.FindByIdAsync(actorId.ToString());
		if (actor == null || !actor.IsActive || actor.TenantId != CurrentTenant.Id) throw new AbpAuthorizationException();
		return principalAccessor.Change(await principals.CreateAsync(actor));
	}
	private async Task<List<StudentPortalAccountLink>> CheckAccountAsync(IdentityUser user)
	{
		if (user.TenantId != CurrentTenant.Id || !user.IsActive) throw Invalid();
		var accountLinks = await links.GetListAsync(x => x.UserId == user.Id);
		var roles = await users.GetRolesAsync(user);
		if (accountLinks.Count == 0 || roles.Count != 1 || accountLinks.Any(x => x.TenantId != CurrentTenant.Id ||
			x.VerifiedByUserId == Guid.Empty || x.Relationship == "guardian" || roles[0] != (x.AccountType == "student" ? "Student" : "Parent")) ||
			roles[0] == "Student" && accountLinks.Count != 1) throw Invalid();
		var studentIds = accountLinks.Select(x => x.StudentId).ToArray();
		if (await accessReviews.AnyAsync(x => Enumerable.Contains(studentIds, x.StudentId) && x.Status == "Pending") ||
			await credentialRequests.AnyAsync(x => x.UserId == user.Id && x.Status == "Processing"))
			throw Invalid("An account review or email delivery is in progress. Complete it before changing the email.");
		foreach (var link in accountLinks)
		{
			var student = await students.GetCredentialsProfileAsync(link.StudentId) ?? throw Invalid();
			await scope.EnsureCanAccessStudentAsync(student, "update portal login email");
			if (!StudentPortalProfileEmail.Same(StudentPortalProfileEmail.Read(student, link.Relationship), user.Email)) throw Invalid();
		}
		return accountLinks;
	}
	private async Task<string> TopologyAsync(List<StudentPortalAccountLink> accountLinks)
	{
		var values = new List<string>();
		foreach (var link in accountLinks.OrderBy(x => x.Id))
		{
			var student = await students.GetCredentialsProfileAsync(link.StudentId) ?? throw Invalid();
			values.Add($"{link.Id:N}:{link.StudentId:N}:{link.UserId:N}:{link.Relationship}:{link.ConcurrencyStamp}:{student.ConcurrencyStamp}:{StudentPortalProfileEmail.Read(student, link.Relationship)?.ToUpperInvariant()}");
		}
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", values))));
	}
	private async Task CheckDuplicateAsync(IdentityUser user, string email)
	{
		var duplicate = await users.FindByEmailAsync(email);
		var duplicateName = await users.FindByNameAsync(email);
		if (duplicate != null && duplicate.Id != user.Id || duplicateName != null && duplicateName.Id != user.Id)
			throw Invalid("This email is already used by another account. Existing accounts cannot be merged through an email change.");
	}
	private async Task CheckRequestSourceAsync(StudentPortalEmailChangeState state, List<StudentPortalAccountLink> accountLinks)
	{
		await LazyServiceProvider.LazyGetRequiredService<IStudentOnboardingAppService>().EnsureCaseAccessAsync(state.CaseId);
		var item = await cases.GetAsync(state.CaseId);
		if (item.TenantId != CurrentTenant.Id || item.Status == StudentOnboardingStatus.Cancelled || !accountLinks.Any(x => x.StudentId == item.StudentId)) throw Invalid();
		if (StudentRecordAdmissionReturnService.ReturnTransitionId(item).HasValue)
			await LazyServiceProvider.LazyGetRequiredService<StudentRecordAdmissionReturnService>().ValidateOnboardingAsync(item, false);
	}
	private static string EditToken(IdentityUser user, string topology) => Hash(user.Id + ":" + user.ConcurrencyStamp + ":" + topology);
	internal static string ProfileToken(Student student) => Hash(student.Id + ":" + student.ConcurrencyStamp);
	private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
	private static BusinessException Invalid(string reason = "The account or email change is unavailable. Refresh App Login and ask authorized staff to review it.") =>
		new BusinessException(StudentOnboardingErrorCodes.PortalAccountInvalid).WithData("Reason", reason);
}

public class StudentPortalEmailChangeJobArgs
{
	public Guid TenantId { get; set; }
	public Guid UserId { get; set; }
	public Guid RequestId { get; set; }
}
