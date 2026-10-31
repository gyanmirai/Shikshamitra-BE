using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Linked_portal_email_unproven_raw_student_fallback_does_not_become_a_student_login_collision() => RunCertificationScenarioAsync(async () =>
	{
		var (fixture, link) = await LinkOnlyEmailOwnerForTestAsync("father", captureStudentEmail: false);
		var email = $"legacy-fallback-{Guid.NewGuid():N}@example.test";
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await _studentRepository.GetCredentialsProfileAsync(fixture.Case.StudentId, trackChanges: true); var contact = student!.ContactDetail!;
			student.ApplyVerifiedRegistrationContact(contact.PrimaryContactNo, contact.SecondaryContactNo, email, contact.Address1, contact.Address2,
				contact.Address3, contact.City, contact.State, contact.Country, contact.Pincode);
			await _studentRepository.UpdateAsync(student, true);
		});
		var service = GetRequiredService<IStudentOnboardingAppService>(); var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		workspace.Recipients.Single(x => x.Relationship == "self").Email.ShouldBeNull();
		await service.UpdateCredentialsEmailAsync(fixture.Case.Id, EmailEdit(workspace, workspace.Recipients.Single(x => x.Relationship == "father"), email));
		await ConfirmEmailForTestAsync(link.UserId, email);
		workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		workspace.Recipients.Single(x => x.Relationship == "self").Email.ShouldBeNull();
		workspace.Recipients.Single(x => x.Relationship == "father").Email.ShouldBe(email);
		(await service.GetPortalAccountsAsync(fixture.Case.Id)).ShouldHaveSingleItem().UserId.ShouldBe(link.UserId);
	});

	[Theory]
	[InlineData("self")]
	[InlineData("father")]
	[InlineData("guardian")]
	public Task Linked_portal_email_request_rejects_opposite_saved_mailbox_without_an_identity(string relationship) => RunCertificationScenarioAsync(async () =>
	{
		var (fixture, link) = await LinkOnlyEmailOwnerForTestAsync(relationship);
		var service = GetRequiredService<IStudentOnboardingAppService>(); var manager = GetRequiredService<IdentityUserManager>();
		var user = await WithUnitOfWorkAsync(() => manager.GetByIdAsync(link.UserId));
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id); var owner = workspace.Recipients.Single(x => x.Relationship == relationship);
		var email = relationship == "self" ? fixture.FatherEmail : fixture.StudentEmail;
		(await WithUnitOfWorkAsync(() => manager.FindByEmailAsync(email))).ShouldBeNull();
		var graph = await CaptureEmailGraphForTestAsync(link.UserId);
		await Should.ThrowAsync<BusinessException>(() => service.UpdateCredentialsEmailAsync(fixture.Case.Id, EmailEdit(workspace, owner, email)));
		var after = await WithUnitOfWorkAsync(() => manager.GetByIdAsync(link.UserId));
		after.Email.ShouldBe(user.Email); after.PasswordHash.ShouldBe(user.PasswordHash);
		after.ExtraProperties.ContainsKey("StudentPortalEmailChange").ShouldBeFalse();
		(await CaptureEmailGraphForTestAsync(link.UserId)).ShouldBe(graph);
	});

	[Fact]
	public Task Linked_portal_email_shared_parent_checks_opposite_mailbox_on_every_linked_child() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync(); var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father");
		await ConfirmSiblingParentOnboardingAsync(fixture, parent);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		await service.ActivateAsync(fixture.Case.Id, new() { ConcurrencyStamp = fixture.Case.ConcurrencyStamp, IdempotencyKey = "shared-opposite-mailbox" });
		var email = $"other-child-{Guid.NewGuid():N}@example.test";
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await _studentRepository.GetCredentialsProfileAsync(parent.StudentId, trackChanges: true);
			StudentPortalProfileEmail.Apply(student!, "self", email, TestUserId); await _studentRepository.UpdateAsync(student!, true);
		});
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id); var owner = workspace.Recipients.Single(x => x.Relationship == "father");
		owner.LinkedStudentCount.ShouldBe(2);
		var input = EmailEdit(workspace, owner, email); input.ConfirmSharedParent = true;
		var graph = await CaptureEmailGraphForTestAsync(parent.UserId);
		await Should.ThrowAsync<BusinessException>(() => service.UpdateCredentialsEmailAsync(fixture.Case.Id, input));
		(await CaptureEmailGraphForTestAsync(parent.UserId)).ShouldBe(graph);
	});

	[Fact]
	public Task Linked_portal_email_verification_send_rechecks_opposite_profile_drift() => RunCertificationScenarioAsync(async () =>
	{
		var (fixture, link) = await LinkOnlyEmailOwnerForTestAsync("self"); var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id); var owner = workspace.Recipients.Single(x => x.Relationship == "self");
		var email = $"send-drift-{Guid.NewGuid():N}@example.test";
		await service.UpdateCredentialsEmailAsync(fixture.Case.Id, EmailEdit(workspace, owner, email));
		var graph = await CaptureEmailGraphForTestAsync(link.UserId);
		await SetOppositeMotherEmailForTestAsync(fixture.Case.StudentId, email);
		(await CaptureEmailGraphForTestAsync(link.UserId)).ShouldBe(graph);
		var (dispatcher, sender, args) = await EmailVerificationDispatcherForTestAsync(link.UserId);
		await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => dispatcher.SendVerificationAsync(args)));
		sender.ReceivedCalls().ShouldBeEmpty();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IdentityUserManager>().GetByIdAsync(link.UserId))).Email.ShouldBe(fixture.StudentEmail);
	});

	[Fact]
	public Task Linked_portal_email_valid_owner_token_rejects_opposite_profile_drift_at_confirmation() => RunCertificationScenarioAsync(async () =>
	{
		var (fixture, link) = await LinkOnlyEmailOwnerForTestAsync("self"); var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id); var owner = workspace.Recipients.Single(x => x.Relationship == "self");
		var email = $"confirm-drift-{Guid.NewGuid():N}@example.test";
		await service.UpdateCredentialsEmailAsync(fixture.Case.Id, EmailEdit(workspace, owner, email));
		var (dispatcher, sender, args) = await EmailVerificationDispatcherForTestAsync(link.UserId);
		await WithUnitOfWorkAsync(() => dispatcher.SendVerificationAsync(args));
		var body = sender.ReceivedCalls().Single(x => x.GetMethodInfo().Name == nameof(IEmailSender.SendAsync)).GetArguments()[2]!.ToString()!;
		var url = System.Net.WebUtility.HtmlDecode(body.Split("href=\"", StringSplitOptions.None)[1].Split('"')[0]);
		var token = Uri.UnescapeDataString(new Uri(url).Query.TrimStart('?').Split('&').Single(x => x.StartsWith("token=", StringComparison.Ordinal))[6..]);
		var graph = await CaptureEmailGraphForTestAsync(link.UserId); var manager = GetRequiredService<IdentityUserManager>();
		var user = await WithUnitOfWorkAsync(() => manager.GetByIdAsync(link.UserId));
		await SetOppositeMotherEmailForTestAsync(fixture.Case.StudentId, email);
		(await CaptureEmailGraphForTestAsync(link.UserId)).ShouldBe(graph);
		await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentPortalEmailChangeAppService>().ConfirmAsync(new()
			{ TenantId = TestTenantId, UserId = link.UserId, RequestId = args.RequestId, Token = token }));
		var after = await WithUnitOfWorkAsync(() => manager.GetByIdAsync(link.UserId));
		after.Email.ShouldBe(fixture.StudentEmail); after.UserName.ShouldBe(user.UserName); after.PasswordHash.ShouldBe(user.PasswordHash);
		(await CaptureEmailGraphForTestAsync(link.UserId)).ShouldBe(graph);
	});

	[Fact]
	public Task Linked_portal_email_adult_contact_sharing_still_verifies_without_replacing_identity_or_password() => RunCertificationScenarioAsync(async () =>
	{
		var (fixture, link) = await LinkOnlyEmailOwnerForTestAsync("father"); var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id); var owner = workspace.Recipients.Single(x => x.Relationship == "father");
		var email = workspace.Recipients.Single(x => x.Relationship == "mother").Email!;
		var manager = GetRequiredService<IdentityUserManager>(); var user = await WithUnitOfWorkAsync(() => manager.GetByIdAsync(link.UserId));
		await service.UpdateCredentialsEmailAsync(fixture.Case.Id, EmailEdit(workspace, owner, email));
		(await WithUnitOfWorkAsync(() => manager.GetByIdAsync(link.UserId))).Email.ShouldBe(fixture.FatherEmail);
		await ConfirmEmailForTestAsync(link.UserId, email);
		workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		workspace.Recipients.Single(x => x.Relationship == "father").Email.ShouldBe(email);
		workspace.Recipients.Single(x => x.Relationship == "mother").Email.ShouldBe(email);
		workspace.Recipients.Single(x => x.Relationship == "self").Email.ShouldBe(fixture.StudentEmail);
		(await service.GetPortalAccountsAsync(fixture.Case.Id)).ShouldHaveSingleItem().UserId.ShouldBe(link.UserId);
		(await WithUnitOfWorkAsync(() => manager.GetByIdAsync(link.UserId))).PasswordHash.ShouldBe(user.PasswordHash);
	});

	private async Task<(OneClickCredentialsFixture Fixture, StudentPortalAccountDto Link)> LinkOnlyEmailOwnerForTestAsync(string relationship, bool captureStudentEmail = true)
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", captureStudentEmail: captureStudentEmail, emailEnabled: false);
		var email = relationship == "self" ? fixture.StudentEmail : fixture.FatherEmail;
		if (relationship == "guardian") await WithUnitOfWorkAsync(async () =>
		{
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>(); var family = await families.GetAsync(fixture.Case.StudentId);
			family.UpdateGuardianDetails(ParentDetails("Saved Guardian", email)); await families.UpdateAsync(family, true);
		});
		var service = GetRequiredService<IStudentOnboardingAppService>(); var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		var link = await service.ProvisionPortalAccountAsync(fixture.Case.Id, new() { AccountType = relationship == "self" ? "student" : "guardian",
			Relationship = relationship, Email = email, RelationshipVerified = true, ConcurrencyStamp = workspace.ConcurrencyStamp });
		await WithUnitOfWorkAsync(async () =>
		{
			var manager = GetRequiredService<IdentityUserManager>(); var user = await manager.GetByIdAsync(link.UserId);
			(await manager.AddPasswordAsync(user, "Owner#29Ab")).Succeeded.ShouldBeTrue();
		});
		return (fixture, link);
	}

	private async Task SetOppositeMotherEmailForTestAsync(Guid studentId, string email) => await WithUnitOfWorkAsync(async () =>
	{
		var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>(); var family = await families.GetAsync(studentId);
		family.UpdateMotherDetails(ParentDetails("Saved Mother", email)); await families.UpdateAsync(family, true);
	});

	private async Task<(StudentPortalEmailChangeService Dispatcher, IEmailSender Sender, StudentPortalEmailChangeJobArgs Args)> EmailVerificationDispatcherForTestAsync(Guid userId)
	{
		var user = await WithUnitOfWorkAsync(() => GetRequiredService<IdentityUserManager>().GetByIdAsync(userId));
		using var state = JsonDocument.Parse(user.ExtraProperties["StudentPortalEmailChange"].ShouldNotBeNull().ToString()!);
		var provider = GetRequiredService<IServiceProvider>(); var sender = Substitute.For<IEmailSender>();
		sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<AdditionalEmailSendingArgs?>()).Returns(Task.CompletedTask);
		var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["App:ClientUrl"] = "https://school.example.test" }).Build();
		var dispatcher = ActivatorUtilities.CreateInstance<StudentPortalEmailChangeService>(provider, sender, config);
		dispatcher.LazyServiceProvider = new AbpLazyServiceProvider(provider);
		return (dispatcher, sender, new() { TenantId = TestTenantId, UserId = userId, RequestId = state.RootElement.GetProperty("RequestId").GetGuid() });
	}
}
