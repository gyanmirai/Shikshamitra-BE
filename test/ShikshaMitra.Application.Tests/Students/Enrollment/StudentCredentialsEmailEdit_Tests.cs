using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("student", "self")]
	[InlineData("guardian", "father")]
	[InlineData("guardian", "mother")]
	public Task Onboarding_email_add_and_update_remains_manual_and_preserves_other_profile_data(string type, string relationship) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", captureStudentEmail: false, emailEnabled: false);
		if (type == "guardian") await WithUnitOfWorkAsync(async () =>
		{
			var familyRepository = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
			var family = await familyRepository.GetAsync(fixture.Case.StudentId);
			var person = relationship == "father" ? family.FatherDetails! : family.MotherDetails!;
			var missingEmail = new ShikshaMitra.Students.ValueObjects.ParentGuardianDetails(person.FullName, person.DateOfBirth, person.Occupation, null,
				person.ContactNo, person.OfficeNo, person.Qualification, person.YearOfPassing, person.Designation, person.Organization,
				person.AadharNo, person.OfficeAddress, person.LocalAddress);
			if (relationship == "father") family.UpdateFatherDetails(missingEmail); else family.UpdateMotherDetails(missingEmail);
			await familyRepository.UpdateAsync(family, true);
		});
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		var owner = workspace.Recipients.Single(x => x.Relationship == relationship);
		owner.CanEditEmail.ShouldBeTrue();
		var before = await WithUnitOfWorkAsync(() => _studentRepository.GetWithDetailsAsync(fixture.Case.StudentId));
		var mobile = before!.ContactDetail?.PrimaryContactNo;
		var parentPhone = before.FamilyDetail?.FatherDetails?.ContactNo;
		var email = $"saved-{Guid.NewGuid():N}@example.test";
		workspace = await service.UpdateCredentialsEmailAsync(workspace.CaseId, EmailEdit(workspace, owner, email));
		owner = workspace.Recipients.Single(x => x.Relationship == relationship);
		owner.Email.ShouldBe(email); owner.CanSend.ShouldBeTrue();
		(await service.GetPortalAccountsAsync(workspace.CaseId)).ShouldBeEmpty();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(x => x.CaseId == workspace.CaseId))).ShouldBe(0);
		workspace = await service.UpdateCredentialsEmailAsync(workspace.CaseId, EmailEdit(workspace, owner, "updated-" + email));
		var recipient = workspace.Recipients.Single(x => x.Relationship == relationship);
		var after = await WithUnitOfWorkAsync(() => _studentRepository.GetWithDetailsAsync(fixture.Case.StudentId));
		after!.ContactDetail?.PrimaryContactNo.ShouldBe(mobile);
		after.FamilyDetail?.FatherDetails?.ContactNo.ShouldBe(parentPhone);
		await service.SendCredentialsAsync(workspace.CaseId, new() { AccountType = type, Relationship = relationship, ConfirmRecipientAccess = type == "guardian",
			ExpectedEmail = recipient.Email!, ConcurrencyStamp = workspace.ConcurrencyStamp });
		var request = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().GetAsync(x => x.CaseId == workspace.CaseId));
		request.Email.ShouldBe("updated-" + email); request.Status.ShouldBe("Pending"); request.AttemptCount.ShouldBe(0);
		request.RequiresSavedOwnerValidation().ShouldBeTrue();
	});

	[Fact]
	public Task Onboarding_email_edit_rejects_stale_profile_invalid_email_tenant_scope_and_permission() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		var owner = workspace.Recipients.Single(x => x.Relationship == "self");
		var input = EmailEdit(workspace, owner, "new@example.test"); input.EmailEditToken = "stale";
		await Should.ThrowAsync<BusinessException>(() => service.UpdateCredentialsEmailAsync(workspace.CaseId, input));
		input = EmailEdit(workspace, owner, "invalid");
		await Should.ThrowAsync<Volo.Abp.Validation.AbpValidationException>(() => service.UpdateCredentialsEmailAsync(workspace.CaseId, input));
		input = EmailEdit(workspace, owner, "new@example.test");
		await Should.ThrowAsync<AbpAuthorizationException>(() => CreateDeniedCredentialsService(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit).UpdateCredentialsEmailAsync(workspace.CaseId, input));
		using (_currentTenant.Change(Guid.NewGuid())) await Should.ThrowAsync<EntityNotFoundException>(() => service.UpdateCredentialsEmailAsync(workspace.CaseId, input));
		workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		workspace.Recipients.Single(x => x.Relationship == "self").Email.ShouldBe(fixture.StudentEmail);
	});

	[Fact]
	public Task Onboarding_email_updates_current_student_family_matches_without_rewriting_admission_answers() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var item = await WithUnitOfWorkAsync(() => _onboardingCaseRepository.GetAsync(fixture.Case.Id));
		var applicant = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(item.ApplicantId));
		(applicant.ExistingStudentId ?? applicant.ReservedStudentId).ShouldBe(fixture.Case.StudentId);
		var originalAnswers = System.Text.Json.JsonSerializer.Serialize(applicant.ExtraProperties);
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		var email = $"current-parent-{Guid.NewGuid():N}@example.test";
		await service.UpdateCredentialsEmailAsync(workspace.CaseId, EmailEdit(workspace, workspace.Recipients.Single(x => x.Relationship == "father"), email));
		var matching = GetRequiredService<AdmissionDirectFamilyMatchService>();
		var matches = await WithUnitOfWorkAsync(() => matching.FindAccessibleMatchesAsync("9999999901", email));
		var match = matches.Single(x => x.FamilyId == applicant.FamilyId);
		match.MatchByEmail.ShouldBeTrue(); match.MatchByMobile.ShouldBeFalse();
		match.Parents.Single(x => x.SourceApplicantId == applicant.Id && x.Relationship == "father").Email.ShouldBe(email);
		(await WithUnitOfWorkAsync(() => matching.FindAccessibleMatchesAsync("9999999901", fixture.FatherEmail))).ShouldBeEmpty();
		System.Text.Json.JsonSerializer.Serialize((await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(applicant.Id))).ExtraProperties).ShouldBe(originalAnswers);
	});

	[Fact]
	public Task Linked_student_email_changes_only_after_owner_verification_without_replacing_password_or_link() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false);
		await LinkOneClickFixtureAccountsAsync(fixture);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		var owner = workspace.Recipients.Single(x => x.Relationship == "self");
		var link = (await service.GetPortalAccountsAsync(workspace.CaseId)).Single(x => x.AccountType == "student");
		var manager = GetRequiredService<IdentityUserManager>();
		await WithUnitOfWorkAsync(async () =>
		{
			var user = await manager.GetByIdAsync(link.UserId);
			(await manager.AddPasswordAsync(user, "Portal#OwnerTest123")).Succeeded.ShouldBeTrue();
			user.SetShouldChangePasswordOnNextLogin(true); (await manager.UpdateAsync(user)).Succeeded.ShouldBeTrue();
		});
		workspace = await service.GetCredentialsAsync(workspace.CaseId);
		await service.SendCredentialsAsync(workspace.CaseId, new() { AccountType = "student", Relationship = "self",
			ExpectedEmail = fixture.StudentEmail, ConcurrencyStamp = workspace.ConcurrencyStamp });
		var deliveries = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
		await WithUnitOfWorkAsync(async () =>
		{
			var row = await deliveries.GetAsync(x => x.CaseId == workspace.CaseId && x.AccountType == "student");
			row.Claim(DateTime.UtcNow); row.Sent(DateTime.UtcNow);
			typeof(StudentCredentialEmailRequest).GetProperty(nameof(StudentCredentialEmailRequest.RequestedAt))!.SetValue(row, DateTime.UtcNow.AddMinutes(-4));
			await deliveries.UpdateAsync(row, true);
		});
		workspace = await service.GetCredentialsAsync(workspace.CaseId); owner = workspace.Recipients.Single(x => x.Relationship == "self");
		var passwordHash = (await WithUnitOfWorkAsync(() => manager.GetByIdAsync(link.UserId))).PasswordHash;
		var email = $"verified-{Guid.NewGuid():N}@example.test";
		var graphBefore = await CaptureEmailGraphForTestAsync(link.UserId);
		workspace = await service.UpdateCredentialsEmailAsync(workspace.CaseId, EmailEdit(workspace, owner, email));
		(await CaptureEmailGraphForTestAsync(link.UserId)).ShouldBe(graphBefore);
		workspace.Recipients.Single(x => x.Relationship == "self").Email.ShouldBe(fixture.StudentEmail);
		workspace.Recipients.Single(x => x.Relationship == "self").PendingEmail.ShouldBe(email);
		await ConfirmEmailForTestAsync(link.UserId, email);
		workspace = await service.GetCredentialsAsync(workspace.CaseId);
		workspace.Recipients.Single(x => x.Relationship == "self").Email.ShouldBe(email);
		var user = await WithUnitOfWorkAsync(() => manager.GetByIdAsync(link.UserId));
		user.PasswordHash.ShouldBe(passwordHash); user.UserName.ShouldBe(email); user.EmailConfirmed.ShouldBeTrue();
		user.ShouldChangePasswordOnNextLogin.ShouldBeTrue();
		(await service.GetPortalAccountsAsync(workspace.CaseId)).Single(x => x.AccountType == "student").UserId.ShouldBe(link.UserId);
		await service.SendCredentialsAsync(workspace.CaseId, new() { AccountType = "student", Relationship = "self", ExpectedEmail = email, ConcurrencyStamp = workspace.ConcurrencyStamp });
		var currentDelivery = await WithUnitOfWorkAsync(() => deliveries.GetAsync(x => x.CaseId == workspace.CaseId && x.AccountType == "student"));
		currentDelivery.Email.ShouldBe(email); currentDelivery.Status.ShouldBe("Pending"); currentDelivery.EncryptedInitialPassword.ShouldBeNull();
		(await WithUnitOfWorkAsync(() => manager.GetByIdAsync(link.UserId))).PasswordHash.ShouldBe(passwordHash);
	});

	[Fact]
	public Task Sibling_parent_email_requires_shared_confirmation_and_updates_both_links_without_new_credentials() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father");
		await ConfirmSiblingParentOnboardingAsync(fixture, parent);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var activated = await service.ActivateAsync(fixture.Case.Id, new() { ConcurrencyStamp = fixture.Case.ConcurrencyStamp,
			IdempotencyKey = "email-change-sibling", SiblingParentSelection = ParentSelection(fixture, parent) });
		var workspace = await service.GetCredentialsAsync(activated.Id);
		var owner = workspace.Recipients.Single(x => x.Relationship == "father");
		owner.LinkedStudentCount.ShouldBe(2);
		var input = EmailEdit(workspace, owner, $"parent-update-{Guid.NewGuid():N}@example.test");
		await Should.ThrowAsync<BusinessException>(() => service.UpdateCredentialsEmailAsync(workspace.CaseId, input));
		await WithUnitOfWorkAsync(() => Should.ThrowAsync<AbpAuthorizationException>(() => CreateDeniedCredentialsService(IdentityPermissions.Users.Update).UpdateCredentialsEmailAsync(workspace.CaseId, input)));
		input.ConfirmSharedParent = true;
		workspace = await service.UpdateCredentialsEmailAsync(workspace.CaseId, input);
		var manager = GetRequiredService<IdentityUserManager>(); var originalHash = (await WithUnitOfWorkAsync(() => manager.GetByIdAsync(parent.UserId))).PasswordHash;
		await ConfirmEmailForTestAsync(parent.UserId, input.Email);
		workspace = await service.GetCredentialsAsync(activated.Id);
		workspace.ParentSibling!.IsLinked.ShouldBeTrue(); workspace.ParentSibling!.Status.ShouldBe("Linked");
		workspace.Recipients.Single(x => x.Relationship == "father").CanSend.ShouldBeFalse();
		var allLinks = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().GetListAsync(x => x.UserId == parent.UserId));
		allLinks.Count.ShouldBe(2);
		foreach (var linked in allLinks)
		{
			var student = await WithUnitOfWorkAsync(() => _studentRepository.GetWithDetailsAsync(linked.StudentId));
			student!.FamilyDetail!.FatherDetails!.Email.ShouldBe(input.Email);
		}
		(await WithUnitOfWorkAsync(() => manager.GetByIdAsync(parent.UserId))).PasswordHash.ShouldBe(originalHash);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(x => x.UserId == parent.UserId))).ShouldBe(0);
	});

	[Fact]
	public Task Onboarding_email_cancellation_invalidates_pending_verification_and_preserves_login() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false);
		await LinkOneClickFixtureAccountsAsync(fixture);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		var owner = workspace.Recipients.Single(x => x.Relationship == "self");
		var link = (await service.GetPortalAccountsAsync(workspace.CaseId)).Single(x => x.AccountType == "student");
		var manager = GetRequiredService<IdentityUserManager>();
		var user = await WithUnitOfWorkAsync(() => manager.GetByIdAsync(link.UserId));
		var password = user.PasswordHash;
		workspace = await service.UpdateCredentialsEmailAsync(workspace.CaseId, EmailEdit(workspace, owner, "cancelled@example.test"));
		user = await WithUnitOfWorkAsync(() => manager.GetByIdAsync(link.UserId));
		using var state = System.Text.Json.JsonDocument.Parse(user.ExtraProperties["StudentPortalEmailChange"].ShouldNotBeNull().ToString()!);
		var requestId = state.RootElement.GetProperty("RequestId").GetGuid();
		var input = EmailEdit(workspace, workspace.Recipients.Single(x => x.Relationship == "self"), string.Empty);
		input.CancelPendingChange = true;
		workspace = await service.UpdateCredentialsEmailAsync(workspace.CaseId, input);
		workspace.Recipients.Single(x => x.Relationship == "self").PendingEmail.ShouldBeNull();
		await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentPortalEmailChangeAppService>().ConfirmAsync(new()
			{ TenantId = TestTenantId, UserId = link.UserId, RequestId = requestId, Token = "cancelled-token" }));
		user = await WithUnitOfWorkAsync(() => manager.GetByIdAsync(link.UserId));
		user.Email.ShouldBe(fixture.StudentEmail); user.PasswordHash.ShouldBe(password);
		(await service.GetPortalAccountsAsync(workspace.CaseId)).Single(x => x.AccountType == "student").UserId.ShouldBe(link.UserId);
	});

	private static UpdateStudentCredentialsEmailDto EmailEdit(StudentCredentialsWorkspaceDto workspace, StudentCredentialsRecipientDto owner, string email) => new()
	{
		AccountType = owner.AccountType, Relationship = owner.Relationship, Email = email, ExpectedEmail = owner.Email,
		ConcurrencyStamp = workspace.ConcurrencyStamp, EmailEditToken = owner.EmailEditToken
	};
	private async Task<string> CaptureEmailGraphForTestAsync(Guid userId) => await WithUnitOfWorkAsync(async () =>
	{
		var accountLinks = await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().GetListAsync(x => x.UserId == userId);
		var parts = new List<object>();
		foreach (var link in accountLinks.OrderBy(x => x.Id))
		{
			var student = await _studentRepository.GetCredentialsProfileAsync(link.StudentId);
			parts.Add(new { link.Id, LinkStamp = link.ConcurrencyStamp, StudentStamp = student!.ConcurrencyStamp,
				EmailHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(StudentPortalProfileEmail.Read(student, link.Relationship) ?? string.Empty))) });
		}
		return System.Text.Json.JsonSerializer.Serialize(parts);
	});
	private async Task ConfirmEmailForTestAsync(Guid userId, string email)
	{
		var manager = GetRequiredService<IdentityUserManager>();
		var user = await WithUnitOfWorkAsync(() => manager.GetByIdAsync(userId));
		using var state = System.Text.Json.JsonDocument.Parse(user.ExtraProperties["StudentPortalEmailChange"].ShouldNotBeNull().ToString()!);
		var requestId = state.RootElement.GetProperty("RequestId").GetGuid();
		var provider = GetRequiredService<IServiceProvider>();
		var sender = Substitute.For<IEmailSender>();
		sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<AdditionalEmailSendingArgs?>()).Returns(Task.CompletedTask);
		var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["App:ClientUrl"] = "https://school.example.test" }).Build();
		var dispatcher = ActivatorUtilities.CreateInstance<StudentPortalEmailChangeService>(provider, sender, config);
		dispatcher.LazyServiceProvider = new AbpLazyServiceProvider(provider);
		var args = new StudentPortalEmailChangeJobArgs { TenantId = TestTenantId, UserId = userId, RequestId = requestId };
		await WithUnitOfWorkAsync(() => dispatcher.SendVerificationAsync(args));
		await WithUnitOfWorkAsync(() => dispatcher.SendVerificationAsync(args));
		var sent = sender.ReceivedCalls().Where(x => x.GetMethodInfo().Name == nameof(IEmailSender.SendAsync)).ToArray();
		sent.Length.ShouldBe(1); sent[0].GetArguments()[0].ShouldBe(email);
		var body = sent[0].GetArguments()[2]!.ToString()!;
		body.Contains("Temporary password", StringComparison.Ordinal).ShouldBeFalse();
		var url = System.Net.WebUtility.HtmlDecode(body.Split("href=\"", StringSplitOptions.None)[1].Split('"')[0]);
		var token = Uri.UnescapeDataString(new Uri(url).Query.TrimStart('?').Split('&').Single(x => x.StartsWith("token=", StringComparison.Ordinal))[6..]);
		var service = GetRequiredService<IStudentPortalEmailChangeAppService>();
		await Should.ThrowAsync<BusinessException>(() => service.ConfirmAsync(new() { TenantId = TestTenantId, UserId = userId, RequestId = requestId, Token = "invalid" }));
		await service.ConfirmAsync(new() { TenantId = TestTenantId, UserId = userId, RequestId = requestId, Token = token });
	}
}
