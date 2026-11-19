using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Portal_email_actor_context_survives_async_loading_for_background_send_and_anonymous_confirmation() => RunCertificationScenarioAsync(async () =>
	{
		var (fixture, link) = await LinkOnlyEmailOwnerForTestAsync("father");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		var email = $"actor-context-{Guid.NewGuid():N}@example.test";
		await service.UpdateCredentialsEmailAsync(fixture.Case.Id, EmailEdit(workspace, workspace.Recipients.Single(x => x.Relationship == "father"), email));
		var (dispatcher, sender, args) = await EmailVerificationDispatcherForTestAsync(link.UserId);
		var current = GetRequiredService<ICurrentUser>();
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(_ => _principalAccessor.Principal);
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
			.Returns(call => Task.FromResult(current.Id == TestUserId &&
				call.ArgAt<ClaimsPrincipal>(0).FindFirst(Volo.Abp.Security.Claims.AbpClaimTypes.UserId)?.Value == TestUserId.ToString()
				? AuthorizationResult.Success() : AuthorizationResult.Failed()));
		dispatcher.LazyServiceProvider = new AbpLazyServiceProvider(new CredentialsAuthorizationProvider(GetRequiredService<IServiceProvider>(), authorization));
		var users = GetRequiredService<IdentityUserManager>();
		var before = await WithUnitOfWorkAsync(async () => (await users.GetByIdAsync(link.UserId)).PasswordHash);
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
		{
			current.Id.ShouldBeNull();
			await WithUnitOfWorkAsync(() => dispatcher.SendVerificationAsync(args));
			current.Id.ShouldBeNull();
			await sender.Received(1).SendAsync(email, Arg.Any<string>(), Arg.Any<string>(), true, Arg.Any<AdditionalEmailSendingArgs?>());
			var token = await WithUnitOfWorkAsync(async () => await users.GenerateChangeEmailTokenAsync(await users.GetByIdAsync(link.UserId), email));
			await WithUnitOfWorkAsync(() => dispatcher.ConfirmAsync(new() { TenantId = TestTenantId, UserId = link.UserId, RequestId = args.RequestId, Token = token }));
			current.Id.ShouldBeNull();
		}
		await WithUnitOfWorkAsync(async () =>
		{
			var user = await users.GetByIdAsync(link.UserId);
			user.Email.ShouldBe(email);
			user.PasswordHash.ShouldBe(before);
		});
	});

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[InlineData(IdentityPermissions.Users.Update)]
	public Task Portal_email_actor_context_still_rejects_current_permission_revocation_before_transport(string permission) => RunCertificationScenarioAsync(async () =>
	{
		var (fixture, link) = await LinkOnlyEmailOwnerForTestAsync("father");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		await service.UpdateCredentialsEmailAsync(fixture.Case.Id, EmailEdit(workspace, workspace.Recipients.Single(x => x.Relationship == "father"), $"revoked-actor-{Guid.NewGuid():N}@example.test"));
		var (dispatcher, sender, args) = await EmailVerificationDispatcherForTestAsync(link.UserId);
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(_ => _principalAccessor.Principal);
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
			.Returns(call => Task.FromResult(call.ArgAt<string>(2) == permission ? AuthorizationResult.Failed() : AuthorizationResult.Success()));
		dispatcher.LazyServiceProvider = new AbpLazyServiceProvider(new CredentialsAuthorizationProvider(GetRequiredService<IServiceProvider>(), authorization));
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
		{
			await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() => dispatcher.SendVerificationAsync(args)));
			GetRequiredService<ICurrentUser>().Id.ShouldBeNull();
		}
		await authorization.Received().AuthorizeAsync(Arg.Is<ClaimsPrincipal>(x => x.FindFirst(Volo.Abp.Security.Claims.AbpClaimTypes.UserId)!.Value == TestUserId.ToString()), Arg.Any<object?>(), permission);
		await sender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<AdditionalEmailSendingArgs?>());
	});
}
