using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Counsellor_lookup_should_scan_many_active_users_and_return_only_scoped_people()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var scopedId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var users = GetRequiredService<IRepository<IdentityUser, Guid>>();
			for (var index = 0; index < 120; index++)
			{
				var id = index == 110 ? scopedId : Guid.NewGuid();
				await users.InsertAsync(new IdentityUser(id,
					$"test-counsellor-{index:000}-{id:N}", $"{id:N}@example.test", TestTenantId));
			}
			await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
				new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: scopedId));
		});

		var choices = await GetRequiredService<IAdmissionIntakeProfileAppService>()
			.GetCounsellorsAsync("test-counsellor-");
		choices.Count.ShouldBe(1);
		choices.Single().Id.ShouldBe(scopedId);
	}

	[Fact]
	public async Task Retired_intake_requirements_should_not_block_capture_confirmation_or_existing_inquiry_start()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		await EnsureSelectableAdmissionUserAsync();
		var service = GetRequiredService<IAdmissionIntakeSetupAppService>();
		var setup = await service.CreateAsync(ValidSetupInput("Simple intake"));
		var workflow = await CreateAndPublishWorkflowAsync();
		var offering = await CreateAvailableOfferingAsync(workflow.Id);
		const string oldRules = "{\"version\":1,\"duplicateRule\":2,\"applicationStartRequirement\":2," +
			"\"requireGuardianEmail\":true,\"requireApplicantDateOfBirth\":true,\"allowedSources\":[10],\"requireSourceDetailFor\":[10]}";
		await WithUnitOfWorkAsync(async () =>
		{
			var profiles = GetRequiredService<IRepository<AdmissionIntakeProfile, Guid>>();
			var profile = await profiles.GetAsync(setup.InquiryIntakeProfileId);
			profile.Update(profile.Code, profile.Name, oldRules);
			await profiles.UpdateAsync(profile, true);
			var current = await GetRequiredService<IRepository<AdmissionOffering, Guid>>().GetAsync(offering.Id);
			current.ConfigureIntake(setup.InquiryIntakeProfileId, setup.CounsellorAssignmentProfileId);
			await GetRequiredService<IRepository<AdmissionOffering, Guid>>().UpdateAsync(current, true);
		});

		var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
		var input = ManagedInquiryInput(offering);
		input.GuardianPhone = "9876543210";
		input.Source = AdmissionInquirySource.QrCode;
		var inquiry = await inquiries.CreateAsync(input);
		inquiry.SourceDetail.ShouldBeNull();
		var duplicate = await Should.ThrowAsync<BusinessException>(() => inquiries.CreateAsync(input));
		duplicate.Code.ShouldBe(AdmissionErrorCodes.InquiryDuplicate);
		input.CreateSeparateFamily = true;
		var noReason = await Should.ThrowAsync<BusinessException>(() => inquiries.CreateAsync(input));
		noReason.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.DuplicateReasonRequired);
		input.DuplicateOverrideReason = "A separate family sharing a contact number.";
		var separate = await inquiries.CreateAsync(input);
		separate.FamilyId.ShouldNotBe(inquiry.FamilyId);

		// Simulate the snapshot already stored by a previous release. Reading it must
		// retire the gates without rewriting the captured policy or its deadlines.
		var states = GetRequiredService<IRepository<AdmissionInquiryManagement, Guid>>();
		const string oldSnapshot = "{\"version\":1,\"enabled\":true,\"applicationStartRequirement\":2,\"requireGuardianEmail\":true}";
		await WithUnitOfWorkAsync(async () =>
		{
			var current = await states.GetAsync(item => item.InquiryId == inquiry.Id);
			typeof(AdmissionInquiryManagement).GetProperty(nameof(AdmissionInquiryManagement.PolicySnapshotJson))!
				.SetValue(current, oldSnapshot);
			await states.UpdateAsync(current, true);
		});
		var management = GetRequiredService<IAdmissionInquiryManagementAppService>();
		var state = await management.GetAsync(inquiry.Id);
		state.CanStartApplication.ShouldBeFalse();
		state.Blockers.ShouldNotBeEmpty();
		state.Policy!.RequireGuardianEmail.ShouldBeFalse();
		state.ContactCount.ShouldBe(0);
		state.Outcome.ShouldBeNull();
		(await states.GetAsync(item => item.InquiryId == inquiry.Id)).PolicySnapshotJson.ShouldBe(oldSnapshot);
		inquiry = await inquiries.UpdateAsync(inquiry.Id, InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested));
		(await management.GetAsync(inquiry.Id)).CanStartApplication.ShouldBeTrue();
		var application = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id);
		application.Id.ShouldNotBe(Guid.Empty);
		(await management.GetAsync(inquiry.Id)).IsLocked.ShouldBeTrue();

		var reloaded = (await service.GetListAsync()).Single(item => item.Id == setup.Id);
		reloaded.InquiryRules.ApplicationStartRequirement.ShouldBe(AdmissionInquiryApplicationStartRequirement.Captured);
		reloaded.InquiryRules.DuplicateRule.ShouldBe(AdmissionInquiryDuplicateRule.RequireReason);
		reloaded.InquiryRules.RequireApplicantDateOfBirth.ShouldBeFalse();
		reloaded.InquiryRules.RequireGuardianEmail.ShouldBeFalse();
		reloaded.InquiryRules.RequireSourceDetailFor.ShouldBeEmpty();
		reloaded.InquiryRules.AllowedSources.ShouldBe([AdmissionInquirySource.QrCode]);
		// A read does not rewrite the historical configuration.
		(await GetRequiredService<IRepository<AdmissionIntakeProfile, Guid>>().GetAsync(setup.InquiryIntakeProfileId))
			.ConfigurationJson.ShouldBe(oldRules);
	}

	[Fact]
	public async Task Setup_writes_should_ignore_retired_requirements_and_counsellor_lookup_should_return_active_scoped_people()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		await EnsureSelectableAdmissionUserAsync();
		var input = ValidSetupInput("Simple intake writes");
		input.InquiryRules.DuplicateRule = AdmissionInquiryDuplicateRule.Block;
		input.InquiryRules.ApplicationStartRequirement = AdmissionInquiryApplicationStartRequirement.Qualified;
		input.InquiryRules.RequireGuardianEmail = true;
		input.InquiryRules.RequireApplicantDateOfBirth = true;
		input.InquiryRules.RequireSourceDetailFor = [AdmissionInquirySource.Referral];
		var created = await GetRequiredService<IAdmissionIntakeSetupAppService>().CreateAsync(input);
		created.InquiryRules.ApplicationStartRequirement.ShouldBe(AdmissionInquiryApplicationStartRequirement.Captured);
		created.InquiryRules.DuplicateRule.ShouldBe(AdmissionInquiryDuplicateRule.RequireReason);
		created.InquiryRules.RequireGuardianEmail.ShouldBeFalse();
		created.InquiryRules.RequireApplicantDateOfBirth.ShouldBeFalse();
		created.InquiryRules.RequireSourceDetailFor.ShouldBeEmpty();
		var profiles = GetRequiredService<IRepository<AdmissionIntakeProfile, Guid>>();
		var saved = JsonSerializer.Deserialize<AdmissionInquiryIntakeRules>((await profiles.GetAsync(created.InquiryIntakeProfileId)).ConfigurationJson,
			new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
		saved.RequireGuardianEmail.ShouldBeFalse();
		saved.RequireSourceDetailFor.ShouldBeEmpty();
		var choices = await GetRequiredService<IAdmissionIntakeProfileAppService>().GetCounsellorsAsync();
		choices.ShouldContain(person => person.Id == TestUserId && !string.IsNullOrWhiteSpace(person.Name));
	}
}
