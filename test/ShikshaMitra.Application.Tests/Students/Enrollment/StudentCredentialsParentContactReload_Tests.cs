using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.ValueObjects;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("father")]
	[InlineData("mother")]
	[InlineData("guardian")]
	public Task Contact_only_parent_profiles_survive_reload_and_remain_saved_credential_options(string relationship) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateAutomaticRegressionFixtureAsync("ContactOnlyParent");
			var finalized = await FinalizeAsync(fixture.Application, true, $"contact-only-{relationship}");
			var email = $"{relationship}-{Guid.NewGuid():N}@example.test";
			await WithUnitOfWorkAsync(async () =>
			{
				var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
				var family = await families.GetAsync(finalized.StudentId);
				var contact = new ParentGuardianDetails(null, null, null, email, null, null, null, null, null, null, null, null, null);
				if (relationship == "father") family.UpdateFatherDetails(contact);
				else if (relationship == "mother") family.UpdateMotherDetails(contact);
				else family.UpdateGuardianDetails(contact);
				await families.UpdateAsync(family, true);
			});
			await WithUnitOfWorkAsync(async () =>
			{
				var student = await _studentRepository.GetWithDetailsAsync(finalized.StudentId);
				var contact = relationship switch
				{
					"father" => student!.FamilyDetail!.FatherDetails,
					"mother" => student!.FamilyDetail!.MotherDetails,
					_ => student!.FamilyDetail!.GuardianDetails
				};
				contact.ShouldNotBeNull();
				contact.FullName.ShouldBeEmpty();
				contact.Email.ShouldBe(email);
			});
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var workspace = await service.GetCredentialsAsync(finalized.OnboardingCaseId);
			var owner = workspace.Recipients.Single(r => r.AccountType == "guardian" && r.Relationship == relationship);
			owner.Email.ShouldBe(email);
			owner.CanSend.ShouldBeFalse();
			// Displaying a saved address must not activate or create a credential delivery.
			(await service.GetAsync(finalized.OnboardingCaseId)).StudentEnrollmentId.ShouldBeNull();
			await Should.ThrowAsync<BusinessException>(() => service.SendCredentialsAsync(workspace.CaseId, new()
			{
				AccountType = "guardian", Relationship = relationship, ExpectedEmail = email,
				ConfirmRecipientAccess = true,
				ConcurrencyStamp = workspace.ConcurrencyStamp
			}));
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>()
				.CountAsync(request => request.CaseId == workspace.CaseId))).ShouldBe(0);
		});
}
