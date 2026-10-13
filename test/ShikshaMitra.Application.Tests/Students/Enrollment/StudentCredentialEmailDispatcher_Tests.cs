using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Security.Encryption;
using Volo.Abp.Uow;
using Xunit;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace ShikshaMitra.Students;

public class StudentCredentialEmailDispatcher_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Sender_failure_retains_initial_password_and_retry_uses_the_same_password()
	{
		using var school = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var fixture = await CreateAsync(false);
		fixture.Sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<AdditionalEmailSendingArgs?>())
			.Returns(Task.FromException(new InvalidOperationException("Transport failed.")));
		await fixture.Dispatcher.DispatchAsync(fixture.Row.Id);
		var failed = await ReadAsync(fixture.Row.Id);
		failed.Status.ShouldBe("Failed"); failed.EncryptedInitialPassword.ShouldNotBeNull();
		var passwordHash = await WithUnitOfWorkAsync(async () => (await GetRequiredService<IdentityUserManager>().FindByIdAsync(fixture.Row.UserId.ToString()))!.PasswordHash);
		await WithUnitOfWorkAsync(async () =>
		{
			var row = await GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().GetAsync(fixture.Row.Id);
			row.Requeue(fixture.Row.RequestedByUserId, DateTime.UtcNow.AddMinutes(3));
			// Move due time back without changing the rate-limit history.
			typeof(StudentCredentialEmailRequest).GetProperty(nameof(StudentCredentialEmailRequest.NextAttemptAt))!.SetValue(row, DateTime.UtcNow.AddMinutes(-1));
			await GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().UpdateAsync(row, true);
		});
		fixture.Sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<AdditionalEmailSendingArgs?>()).Returns(Task.CompletedTask);
		await fixture.Dispatcher.DispatchAsync(fixture.Row.Id);
		var sent = await ReadAsync(fixture.Row.Id); sent.Status.ShouldBe("Sent"); sent.AttemptCount.ShouldBe(2); sent.EncryptedInitialPassword.ShouldBeNull();
		(await WithUnitOfWorkAsync(async () => (await GetRequiredService<IdentityUserManager>().FindByIdAsync(fixture.Row.UserId.ToString()))!.PasswordHash)).ShouldBe(passwordHash);
	}
	[Fact]
	public async Task Linking_a_sibling_preserves_the_existing_parent_password()
	{
		using var school = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var fixture = await CreateAsync(true);
		var before = await WithUnitOfWorkAsync(async () => (await GetRequiredService<IdentityUserManager>().FindByIdAsync(fixture.Row.UserId.ToString()))!.PasswordHash);
		await fixture.Dispatcher.DispatchAsync(fixture.Row.Id);
		(await ReadAsync(fixture.Row.Id)).Status.ShouldBe("Sent");
		(await WithUnitOfWorkAsync(async () => (await GetRequiredService<IdentityUserManager>().FindByIdAsync(fixture.Row.UserId.ToString()))!.PasswordHash)).ShouldBe(before);
		var body = fixture.Sender.ReceivedCalls().Single(x => x.GetMethodInfo().Name == nameof(IEmailSender.SendAsync)).GetArguments()[2]?.ToString();
		(body?.Contains("existing password", StringComparison.Ordinal) == true).ShouldBeTrue();
		(body?.Contains("Temporary password:", StringComparison.Ordinal) == true).ShouldBeFalse();
	}
	[Fact]
	public async Task Revoked_link_cancels_pending_credentials_without_transport_or_password_creation()
	{
		using var school = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var fixture = await CreateAsync(false);
		fixture.Resolver.ResolveAsync(fixture.Row.CaseId, "guardian", false).Returns(fixture.State with { IsReady = false, LinkId = null });
		await fixture.Dispatcher.DispatchAsync(fixture.Row.Id);
		(await ReadAsync(fixture.Row.Id)).Status.ShouldBe("Cancelled");
		fixture.Sender.ReceivedCalls().ShouldBeEmpty();
		(await WithUnitOfWorkAsync(async () => (await GetRequiredService<IdentityUserManager>().FindByIdAsync(fixture.Row.UserId.ToString()))!.PasswordHash)).ShouldBeNull();
	}
	[Fact]
	public async Task Cancelled_initial_credentials_can_recover_via_setup_link_without_replacing_the_password()
	{
		using var school = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var fixture = await CreateAsync(true);
		await WithUnitOfWorkAsync(async () =>
		{
			var manager = GetRequiredService<IdentityUserManager>(); var user = await manager.FindByIdAsync(fixture.Row.UserId.ToString());
			user!.SetShouldChangePasswordOnNextLogin(true); (await manager.UpdateAsync(user)).Succeeded.ShouldBeTrue();
			var row = await GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().GetAsync(fixture.Row.Id);
			row.Cancel("School email disabled."); row.Requeue(fixture.Row.RequestedByUserId, DateTime.UtcNow.AddMinutes(-1));
			await GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().UpdateAsync(row, true);
		});
		var before = await WithUnitOfWorkAsync(async () => (await GetRequiredService<IdentityUserManager>().FindByIdAsync(fixture.Row.UserId.ToString()))!.PasswordHash);
		await fixture.Dispatcher.DispatchAsync(fixture.Row.Id);
		(await ReadAsync(fixture.Row.Id)).Status.ShouldBe("Sent");
		var body = fixture.Sender.ReceivedCalls().Single(x => x.GetMethodInfo().Name == nameof(IEmailSender.SendAsync)).GetArguments()[2]?.ToString();
		(body?.Contains("secure setup link", StringComparison.Ordinal) == true).ShouldBeTrue();
		(body?.Contains("/account/reset-password", StringComparison.Ordinal) == true).ShouldBeTrue();
		(body?.Contains("Temporary password:", StringComparison.Ordinal) == true).ShouldBeFalse();
		(await WithUnitOfWorkAsync(async () => (await GetRequiredService<IdentityUserManager>().FindByIdAsync(fixture.Row.UserId.ToString()))!.PasswordHash)).ShouldBe(before);
	}
	[Fact]
	public async Task A_completed_request_is_not_sent_again_by_another_worker_run()
	{
		using var school = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var fixture = await CreateAsync(true);
		await fixture.Dispatcher.DispatchAsync(fixture.Row.Id);
		await fixture.Dispatcher.DispatchAsync(fixture.Row.Id);
		fixture.Sender.ReceivedCalls().Count(x => x.GetMethodInfo().Name == nameof(IEmailSender.SendAsync)).ShouldBe(1);
		(await ReadAsync(fixture.Row.Id)).AttemptCount.ShouldBe(1);
	}
	[Fact]
	public async Task Another_school_cannot_dispatch_the_request()
	{
		var tenant = GetRequiredService<ICurrentTenant>();
		using var school = tenant.Change(Guid.NewGuid());
		var fixture = await CreateAsync(false);
		using (tenant.Change(Guid.NewGuid())) await fixture.Dispatcher.DispatchAsync(fixture.Row.Id);
		(await ReadAsync(fixture.Row.Id)).Status.ShouldBe("Pending"); fixture.Sender.ReceivedCalls().ShouldBeEmpty();
	}
	[Fact]
	public async Task Revoked_staff_academic_scope_cancels_the_queued_email()
	{
		using var school = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var fixture = await CreateAsync(false);
		fixture.Onboarding.EnsureCaseAccessAsync(fixture.Row.CaseId).Returns(Task.FromException(new AbpAuthorizationException()));
		await fixture.Dispatcher.DispatchAsync(fixture.Row.Id);
		(await ReadAsync(fixture.Row.Id)).Status.ShouldBe("Cancelled"); fixture.Sender.ReceivedCalls().ShouldBeEmpty();
	}
	[Fact]
	public async Task Revoked_staff_permission_cancels_the_queued_email()
	{
		using var school = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var fixture = await CreateAsync(false); fixture.Permissions.IsGrantedAsync(Arg.Any<string>()).Returns(false);
		await fixture.Dispatcher.DispatchAsync(fixture.Row.Id);
		(await ReadAsync(fixture.Row.Id)).Status.ShouldBe("Cancelled"); fixture.Sender.ReceivedCalls().ShouldBeEmpty();
	}
	[Fact]
	public async Task Completion_timing_defers_pending_email_without_password_until_other_accounts_are_prepared()
	{
		using var school = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var fixture = await CreateAsync(false);
		fixture.Resolver.ResolveAsync(fixture.Row.CaseId, "guardian", false).Returns(fixture.State with
			{ IsReady = false, WaitingForCompletion = true, BlockedReason = "Full onboarding completion is pending." });
		await fixture.Dispatcher.DispatchAsync(fixture.Row.Id);
		var deferred = await ReadAsync(fixture.Row.Id);
		deferred.Status.ShouldBe("Pending"); deferred.AttemptCount.ShouldBe(0); deferred.NextAttemptAt.ShouldBeGreaterThan(DateTime.UtcNow);
		fixture.Sender.ReceivedCalls().ShouldBeEmpty();
		(await WithUnitOfWorkAsync(async () => (await GetRequiredService<IdentityUserManager>().FindByIdAsync(fixture.Row.UserId.ToString()))!.PasswordHash)).ShouldBeNull();
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>(); var row = await repository.GetAsync(fixture.Row.Id);
			typeof(StudentCredentialEmailRequest).GetProperty(nameof(StudentCredentialEmailRequest.NextAttemptAt))!.SetValue(row, DateTime.UtcNow.AddMinutes(-1));
			await repository.UpdateAsync(row, true);
		});
		fixture.Resolver.ResolveAsync(fixture.Row.CaseId, "guardian", false).Returns(fixture.State);
		await fixture.Dispatcher.DispatchAsync(fixture.Row.Id);
		(await ReadAsync(fixture.Row.Id)).Status.ShouldBe("Sent");
		fixture.Sender.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IEmailSender.SendAsync)).ShouldBe(1);
	}
	[Fact]
	public async Task Changed_recipient_during_completion_wait_is_cancelled_instead_of_deferred()
	{
		using var school = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var fixture = await CreateAsync(false);
		fixture.Resolver.ResolveAsync(fixture.Row.CaseId, "guardian", false).Returns(fixture.State with
			{ IsReady = false, WaitingForCompletion = true, Email = "different@example.test" });
		await fixture.Dispatcher.DispatchAsync(fixture.Row.Id);
		(await ReadAsync(fixture.Row.Id)).Status.ShouldBe("Cancelled"); fixture.Sender.ReceivedCalls().ShouldBeEmpty();
	}
	[Fact]
	public async Task Explicit_saved_owner_intent_fails_closed_when_provenance_can_no_longer_be_verified()
	{
		using var school = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var fixture = await CreateAsync(false);
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>(); var row = await repository.GetAsync(fixture.Row.Id);
			row.RequireSavedOwnerValidation(); await repository.UpdateAsync(row, true);
		});
		// This fixture has no verifier. Explicit saved-owner intents must fail closed.
		await fixture.Dispatcher.DispatchAsync(fixture.Row.Id);
		(await ReadAsync(fixture.Row.Id)).Status.ShouldBe("Cancelled"); fixture.Sender.ReceivedCalls().ShouldBeEmpty();
		(await WithUnitOfWorkAsync(async () => (await GetRequiredService<IdentityUserManager>().FindByIdAsync(fixture.Row.UserId.ToString()))!.PasswordHash)).ShouldBeNull();
	}
	[Fact]
	public async Task Only_a_marked_manual_intent_uses_explicit_eligibility_in_both_dispatch_phases()
	{
		using var school = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var recipients = Substitute.For<IStudentCredentialRecipientVerifier>();
		recipients.IsCurrentOwnerAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>()).Returns(true);
		var fixture = await CreateAsync(true, recipients);
		fixture.Resolver.ResolveAsync(fixture.Row.CaseId, "guardian", false).Returns(fixture.State with
			{ EmailEnabled = false, IsReady = false, BlockedReason = "Automatic email is disabled." });
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>(); var row = await repository.GetAsync(fixture.Row.Id);
			row.RequireSavedOwnerValidation(); await repository.UpdateAsync(row, true);
		});
		await fixture.Dispatcher.DispatchAsync(fixture.Row.Id);
		(await ReadAsync(fixture.Row.Id)).Status.ShouldBe("Sent");
		await fixture.Resolver.Received(2).ResolveExplicitAsync(fixture.Row.CaseId, "guardian", false);
		await fixture.Resolver.DidNotReceive().ResolveAsync(fixture.Row.CaseId, "guardian", false);
	}
	[Fact]
	public async Task An_unmarked_automatic_intent_cannot_bypass_disabled_school_email_flags()
	{
		using var school = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var fixture = await CreateAsync(false);
		fixture.Resolver.ResolveAsync(fixture.Row.CaseId, "guardian", false).Returns(fixture.State with
			{ EmailEnabled = false, IsReady = false, BlockedReason = "Automatic email is disabled." });
		await fixture.Dispatcher.DispatchAsync(fixture.Row.Id);
		(await ReadAsync(fixture.Row.Id)).Status.ShouldBe("Cancelled"); fixture.Sender.ReceivedCalls().ShouldBeEmpty();
		await fixture.Resolver.DidNotReceive().ResolveExplicitAsync(fixture.Row.CaseId, "guardian", false);
	}
	private Task<StudentCredentialEmailRequest> ReadAsync(Guid id) => WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().GetAsync(id));
	private async Task<Fixture> CreateAsync(bool existingPassword, IStudentCredentialRecipientVerifier? recipients = null)
	{
		var tenant = GetRequiredService<ICurrentTenant>(); var users = GetRequiredService<IdentityUserManager>();
		var actor = new IdentityUser(Guid.NewGuid(), "staff" + Guid.NewGuid().ToString("N"), "staff" + Guid.NewGuid().ToString("N") + "@example.test", tenant.Id);
		var parent = new IdentityUser(Guid.NewGuid(), "parent" + Guid.NewGuid().ToString("N"), "parent" + Guid.NewGuid().ToString("N") + "@example.test", tenant.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			(await users.CreateAsync(actor)).Succeeded.ShouldBeTrue();
			(await (existingPassword ? users.CreateAsync(parent, "Existing!Password123") : users.CreateAsync(parent))).Succeeded.ShouldBeTrue();
		});
		var row = new StudentCredentialEmailRequest(Guid.NewGuid(), tenant.Id!.Value, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), parent.Id,
			"guardian", parent.Email!, actor.Id, DateTime.UtcNow.AddMinutes(-3));
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().InsertAsync(row, true));
		var state = new StudentCredentialEmailEligibility(row.CaseId, row.StudentId, row.LinkId, row.UserId, row.Email, "Child", true, true, null, "https://school.example.test/login", "Example school");
		var resolver = Substitute.For<IStudentCredentialEmailEligibilityResolver>(); resolver.ResolveAsync(row.CaseId, "guardian", false).Returns(state);
		resolver.ResolveExplicitAsync(row.CaseId, "guardian", false).Returns(state);
		var sender = Substitute.For<IEmailSender>(); var permissions = Substitute.For<IPermissionChecker>(); permissions.IsGrantedAsync(Arg.Any<string>()).Returns(true);
		var onboarding = Substitute.For<IStudentOnboardingAppService>(); onboarding.EnsureCaseAccessAsync(row.CaseId).Returns(Task.CompletedTask);
		var dispatcher = new StudentCredentialEmailDispatcher(GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>(), resolver, users, sender,
			GetRequiredService<IStringEncryptionService>(), GetRequiredService<IUnitOfWorkManager>(), tenant,
			GetRequiredService<IUserClaimsPrincipalFactory<IdentityUser>>(), GetRequiredService<ICurrentPrincipalAccessor>(), permissions,
			onboarding, recipients);
		return new(row, state, resolver, sender, permissions, dispatcher, onboarding);
	}
	private record Fixture(StudentCredentialEmailRequest Row, StudentCredentialEmailEligibility State,
		IStudentCredentialEmailEligibilityResolver Resolver, IEmailSender Sender, IPermissionChecker Permissions, StudentCredentialEmailDispatcher Dispatcher,
		IStudentOnboardingAppService Onboarding);
}
