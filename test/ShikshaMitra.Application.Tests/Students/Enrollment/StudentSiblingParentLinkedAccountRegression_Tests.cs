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
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Sibling_parent_linked_account_first_manual_credentials_work_when_confirmed_family_has_no_portal_account() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var sourceStudent = await CreateExistingStudentAsync(fixture.Offering, "Existing", "SiblingWithoutLogin");
		var sourceId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(fixture.ApplicantId);
			var source = new AdmissionApplicant(sourceId, TestTenantId, applicant.FamilyId, "Existing", null,
				"SiblingWithoutLogin", DateTime.Today.AddYears(-8), "Male", sourceStudent.Id, Guid.NewGuid());
			AdmissionSiblingParentSelection.CaptureParent(source, "father", "Sibling Father", "9000000002", fixture.FatherEmail);
			await _applicantRepository.InsertAsync(source, true);
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
			var family = await families.FindAsync(sourceStudent.Id)
				?? await families.InsertAsync(new StudentFamilyDetail(sourceStudent.Id, TestTenantId), true);
			family.UpdateFatherDetails(ParentDetails("Sibling Father", fixture.FatherEmail));
			await families.UpdateAsync(family, true);
		});
		await ConfirmSiblingParentOnboardingAsync(fixture, new(sourceId, sourceStudent.Id, Guid.Empty, Guid.Empty, "father"));
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var activated = await service.ActivateAsync(fixture.Case.Id, new()
		{
			ConcurrencyStamp = fixture.Case.ConcurrencyStamp, IdempotencyKey = "first-family-parent-login"
		});
		var before = await service.GetCredentialsAsync(activated.Id);
		before.Recipients.Single(owner => owner.Relationship == "father").CanSend.ShouldBeTrue();
		await service.SendCredentialsAsync(activated.Id, new()
		{
			AccountType = "guardian", Relationship = "father", ConfirmRecipientAccess = true,
			ExpectedEmail = fixture.FatherEmail, ConcurrencyStamp = before.ConcurrencyStamp
		});
		var after = await service.GetCredentialsAsync(activated.Id);
		after.ParentSibling!.BlockedReason.ShouldBeNull();
		after.Recipients.Single(owner => owner.Relationship == "father").BlockedReason.ShouldNotBe(
			"The linked parent differs from the confirmed family. Ask an administrator to review it.");
		var account = (await service.GetPortalAccountsAsync(activated.Id)).ShouldHaveSingleItem();
		account.Relationship.ShouldBe("father");
		var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
		var request = await WithUnitOfWorkAsync(() => requests.GetAsync(row => row.CaseId == activated.Id && row.AccountType == "guardian"));
		request.Status.ShouldBe("Pending");
		request.UserId.ShouldBe(account.UserId);
		request.RequiresSavedOwnerValidation().ShouldBeTrue();
		await service.SendCredentialsAsync(activated.Id, new()
		{
			AccountType = "guardian", Relationship = "father", ConfirmRecipientAccess = true,
			ExpectedEmail = fixture.FatherEmail, ConcurrencyStamp = after.ConcurrencyStamp
		});
		(await WithUnitOfWorkAsync(() => requests.CountAsync(row => row.CaseId == activated.Id && row.AccountType == "guardian"))).ShouldBe(1);
		var siblingService = GetRequiredService<StudentSiblingParentService>();
		(await WithUnitOfWorkAsync(async () => siblingService.WasReused(await _onboardingCaseRepository.GetAsync(activated.Id)))).ShouldBeFalse();
		var sender = Substitute.For<IEmailSender>();
		var provider = GetRequiredService<IServiceProvider>();
		// As in the sibling retry regression, the test module needs an absolute
		// browser origin for setup-link rendering; eligibility remains the real service.
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{ ["App:ClientUrl"] = "https://school.example.test" }).Build();
		var resolver = ActivatorUtilities.CreateInstance<StudentOnboardingCredentialEligibilityResolver>(provider, configuration);
		var dispatcher = ActivatorUtilities.CreateInstance<StudentCredentialEmailDispatcher>(provider, sender, resolver);
		await dispatcher.DispatchAsync(request.Id);
		(await WithUnitOfWorkAsync(() => requests.GetAsync(request.Id))).Status.ShouldBe("Sent");
		sender.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IEmailSender.SendAsync)).ShouldBe(1);
	});

	[Fact]
	public Task Sibling_parent_linked_account_remains_valid_after_the_source_childs_portal_link_is_removed() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father");
		await ConfirmSiblingParentOnboardingAsync(fixture, parent);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var activated = await service.ActivateAsync(fixture.Case.Id, new()
		{
			ConcurrencyStamp = fixture.Case.ConcurrencyStamp, IdempotencyKey = "independent-child-parent-link",
			SiblingParentSelection = ParentSelection(fixture, parent)
		});
		var manager = GetRequiredService<IdentityUserManager>();
		var passwordHash = (await WithUnitOfWorkAsync(() => manager.GetByIdAsync(parent.UserId))).PasswordHash;
		var links = GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>();
		var targetLink = await WithUnitOfWorkAsync(() => links.GetAsync(link => link.StudentId == activated.StudentId && link.AccountType == "guardian"));
		await WithUnitOfWorkAsync(() => links.DeleteAsync(parent.LinkId, true));
		var workspace = await service.GetCredentialsAsync(activated.Id);
		workspace.ParentSibling!.BlockedReason.ShouldBeNull();
		workspace.ParentSibling.IsLinked.ShouldBeTrue();
		workspace.ParentSibling.Status.ShouldBe("Linked");
		workspace.Recipients.Single(owner => owner.Relationship == "father").CanSend.ShouldBeFalse();
		var retained = await WithUnitOfWorkAsync(() => links.GetAsync(targetLink.Id));
		retained.UserId.ShouldBe(parent.UserId);
		retained.Relationship.ShouldBe("father");
		(await WithUnitOfWorkAsync(() => manager.GetByIdAsync(parent.UserId))).PasswordHash.ShouldBe(passwordHash);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(row => row.CaseId == activated.Id))).ShouldBe(0);
		using (_principalAccessor.Change(EligibilityPrincipal(parent.UserId, fixture.FatherEmail, "Parent")))
		{
			var accessible = await service.GetMyPortalStudentsAsync();
			accessible.ShouldContain(account => account.StudentId == activated.StudentId);
			accessible.ShouldNotContain(account => account.StudentId == parent.StudentId);
		}
	});

	[Fact]
	public Task Sibling_parent_linked_account_still_rejects_owner_identity_relationship_and_selection_drift() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father");
		await ConfirmSiblingParentOnboardingAsync(fixture, parent);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var activated = await service.ActivateAsync(fixture.Case.Id, new()
		{
			ConcurrencyStamp = fixture.Case.ConcurrencyStamp, IdempotencyKey = "retained-parent-owner-guards",
			SiblingParentSelection = ParentSelection(fixture, parent)
		});
		var links = GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>();
		var manager = GetRequiredService<IdentityUserManager>();
		var passwordHash = (await WithUnitOfWorkAsync(() => manager.GetByIdAsync(parent.UserId))).PasswordHash;
		var target = await WithUnitOfWorkAsync(() => links.GetAsync(link => link.StudentId == activated.StudentId && link.AccountType == "guardian"));
		await WithUnitOfWorkAsync(() => links.DeleteAsync(parent.LinkId, true));
		foreach (var drift in new[] { "inactive", "role", "email", "verification", "relationship", "selection" })
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var user = await manager.GetByIdAsync(parent.UserId);
				var link = await links.GetAsync(target.Id);
				var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
				var family = await families.GetAsync(activated.StudentId);
				var item = await _onboardingCaseRepository.GetAsync(activated.Id);
				var savedSelection = item.GetProperty<string>("SiblingParentActivationSelection");
				if (drift == "inactive") { user.SetIsActive(false); (await manager.UpdateAsync(user)).Succeeded.ShouldBeTrue(); }
				if (drift == "role") (await manager.RemoveFromRoleAsync(user, "Parent")).Succeeded.ShouldBeTrue();
				if (drift == "email") { family.UpdateFatherDetails(ParentDetails("Changed father", "changed-owner@example.test")); await families.UpdateAsync(family, true); }
				if (drift == "verification") { typeof(StudentPortalAccountLink).GetProperty(nameof(StudentPortalAccountLink.VerifiedByUserId))!.SetValue(link, Guid.Empty); await links.UpdateAsync(link, true); }
				if (drift == "relationship") { typeof(StudentPortalAccountLink).GetProperty(nameof(StudentPortalAccountLink.Relationship))!.SetValue(link, "mother"); await links.UpdateAsync(link, true); }
				if (drift == "selection")
				{
					item.SetProperty("SiblingParentActivationSelection", JsonSerializer.Serialize(new StudentSiblingParentSelectionDto
					{ Relationship = "father", ExpectedEmail = fixture.FatherEmail, ExpectedUserId = Guid.NewGuid() }));
					await _onboardingCaseRepository.UpdateAsync(item, true);
				}
				var context = await GetRequiredService<StudentSiblingParentService>().GetAsync(item);
				context.Status.ShouldBe("ReviewRequired", drift);
				context.IsLinked.ShouldBeFalse(drift);
				context.BlockedReason.ShouldNotBeNull(drift);
				if (drift == "inactive") { user.SetIsActive(true); (await manager.UpdateAsync(user)).Succeeded.ShouldBeTrue(); }
				if (drift == "role") (await manager.AddToRoleAsync(user, "Parent")).Succeeded.ShouldBeTrue();
				if (drift == "email") { family.UpdateFatherDetails(ParentDetails("Sibling Father", fixture.FatherEmail)); await families.UpdateAsync(family, true); }
				if (drift == "verification") { typeof(StudentPortalAccountLink).GetProperty(nameof(StudentPortalAccountLink.VerifiedByUserId))!.SetValue(link, TestUserId); await links.UpdateAsync(link, true); }
				if (drift == "relationship") { typeof(StudentPortalAccountLink).GetProperty(nameof(StudentPortalAccountLink.Relationship))!.SetValue(link, "father"); await links.UpdateAsync(link, true); }
				if (drift == "selection") { item.SetProperty("SiblingParentActivationSelection", savedSelection); await _onboardingCaseRepository.UpdateAsync(item, true); }
			});
			(await service.GetCredentialsAsync(activated.Id)).ParentSibling!.IsLinked.ShouldBeTrue(drift);
		}
		(await WithUnitOfWorkAsync(() => manager.GetByIdAsync(parent.UserId))).PasswordHash.ShouldBe(passwordHash);
		(await WithUnitOfWorkAsync(() => links.CountAsync(link => link.StudentId == activated.StudentId && link.AccountType == "guardian"))).ShouldBe(1);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(row => row.CaseId == activated.Id))).ShouldBe(0);
	});
}
