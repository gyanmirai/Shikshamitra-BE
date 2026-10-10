using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(-1)]
	[InlineData(0)]
	public Task Public_link_should_explain_expiry_at_or_before_now(int minutesFromNow) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var setup = await GetRequiredService<IAdmissionIntakeSetupAppService>()
				.CreateAsync(ValidSetupInput("Expiry readiness"));
			var input = await LinkInputAsync(offering, setup);
			input.ExpiresAtUtc = GetRequiredService<IClock>().Now.ToUniversalTime().AddMinutes(minutesFromNow);
			var error = await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<IAdmissionPublicInquiryAppService>().CreateLinkAsync(input));
			error.Code.ShouldBe(AdmissionErrorCodes.PublicInquiryExpiryInvalid);
			(await GetRequiredService<IRepository<AdmissionPublicInquiryLink, Guid>>()
				.GetListAsync(item => item.AdmissionCycleId == offering.AdmissionCycleId)).ShouldBeEmpty();
		});

	[Theory]
	[InlineData(false, AdmissionEntryMode.Inquiry)]
	[InlineData(true, AdmissionEntryMode.DirectRegistration)]
	public Task Public_link_should_explain_missing_active_inquiry_offering(bool active, AdmissionEntryMode entryMode) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var setup = await GetRequiredService<IAdmissionIntakeSetupAppService>()
				.CreateAsync(ValidSetupInput("Link readiness"));
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(offering.Id);
				stored.UpdateRouting(stored.Track, entryMode, identityLocked: false);
				if (!active) stored.Deactivate();
				await _offeringRepository.UpdateAsync(stored, autoSave: true);
			});
			var input = await LinkInputAsync(offering, setup);
			var error = await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<IAdmissionPublicInquiryAppService>().CreateLinkAsync(input));
			error.Code.ShouldBe(AdmissionErrorCodes.PublicInquiryOfferingRequired);
			error.Data["PeriodName"].ShouldBe((await _cycleRepository.GetAsync(offering.AdmissionCycleId)).Name);
			error.Data["Reason"].ShouldBe("NoActiveInquiryOffering");
			error.Message.ShouldContain("Configure and activate an Offering");
			(await GetRequiredService<IRepository<AdmissionPublicInquiryLink, Guid>>()
				.GetListAsync(item => item.AdmissionCycleId == offering.AdmissionCycleId)).ShouldBeEmpty();
		});

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public Task Public_link_should_explain_inactive_or_unscoped_counsellor(bool active) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var setup = await GetRequiredService<IAdmissionIntakeSetupAppService>()
				.CreateAsync(ValidSetupInput("Counsellor readiness"));
			var userId = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(userId, $"link-owner-{userId:N}");
			if (!active)
				await WithUnitOfWorkAsync(async () =>
				{
					await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
						userId: userId, description: "Inactive link owner"), autoSave: true);
					var users = GetRequiredService<IRepository<IdentityUser, Guid>>();
					var user = await users.GetAsync(userId);
					user.SetIsActive(false);
					await users.UpdateAsync(user, autoSave: true);
				});
			await SetLinkAssignmentRulesAsync(setup, userId, TestUserId);
			var input = await LinkInputAsync(offering, setup);
			var error = await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<IAdmissionPublicInquiryAppService>().CreateLinkAsync(input));
			error.Code.ShouldBe(AdmissionErrorCodes.PublicInquiryCounsellorUnavailable);
			error.Data["SetupName"].ShouldBe(setup.Name);
		});

	[Fact]
	public Task Public_link_should_explain_unscoped_fallback_even_when_pool_is_eligible() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var setup = await GetRequiredService<IAdmissionIntakeSetupAppService>()
				.CreateAsync(ValidSetupInput("Fallback readiness"));
			var fallbackId = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(fallbackId, $"link-fallback-{fallbackId:N}");
			await SetLinkAssignmentRulesAsync(setup, TestUserId, fallbackId);
			var input = await LinkInputAsync(offering, setup);
			var error = await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<IAdmissionPublicInquiryAppService>().CreateLinkAsync(input));
			error.Code.ShouldBe(AdmissionErrorCodes.PublicInquiryFallbackUnavailable);
			error.Data["SetupName"].ShouldBe(setup.Name);
		});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Public_link_should_still_create_with_eligible_pool_and_fallback(bool withExpiry) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var setup = await GetRequiredService<IAdmissionIntakeSetupAppService>()
				.CreateAsync(ValidSetupInput("Ready intake"));
			var input = await LinkInputAsync(offering, setup);
			if (withExpiry)
				input.ExpiresAtUtc = GetRequiredService<IClock>().Now.ToUniversalTime().AddDays(1);
			var created = await GetRequiredService<IAdmissionPublicInquiryAppService>().CreateLinkAsync(input);
			created.IsActive.ShouldBeTrue();
			created.ExpiresAtUtc.ShouldBe(input.ExpiresAtUtc);
			created.Token.Length.ShouldBe(64);
			var stored = (await GetRequiredService<IRepository<AdmissionPublicInquiryLink, Guid>>()
				.GetListAsync(item => item.AdmissionCycleId == offering.AdmissionCycleId)).Single();
			stored.TokenHash.ShouldNotBe(created.Token);
		});

	[Theory]
	[InlineData(true, "SetupNotLinked")]
	[InlineData(false, "FormNotLinked")]
	public Task Public_link_should_explain_unlinked_setup_or_form_and_allow_corrected_retry(bool changeSetup, string reason) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var setupService = GetRequiredService<IAdmissionIntakeSetupAppService>();
			var setup = await setupService.CreateAsync(ValidSetupInput("Linked setup"));
			var input = await LinkInputAsync(offering, setup);
			var correctProfileId = input.CounsellorAssignmentProfileId;
			var correctFormId = input.InquiryFormDefinitionId;
			if (changeSetup)
				input.CounsellorAssignmentProfileId = (await setupService.CreateAsync(ValidSetupInput("Unlinked setup"))).CounsellorAssignmentProfileId;
			else
				input.InquiryFormDefinitionId = (await CreateOptionalIntakeOfferingAsync()).InquiryFormDefinitionId;
			var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
			var error = await Should.ThrowAsync<BusinessException>(() => service.CreateLinkAsync(input));
			error.Code.ShouldBe(AdmissionErrorCodes.PublicInquiryOfferingRequired);
			error.Data["Reason"].ShouldBe(reason);
			error.Message.ShouldContain("Attach this setup and form to the same Offering");
			(await GetRequiredService<IRepository<AdmissionPublicInquiryLink, Guid>>()
				.GetListAsync(item => item.AdmissionCycleId == offering.AdmissionCycleId)).ShouldBeEmpty();
			input.CounsellorAssignmentProfileId = correctProfileId;
			input.InquiryFormDefinitionId = correctFormId;
			(await service.CreateLinkAsync(input)).IsActive.ShouldBeTrue();
		});

	[Fact]
	public Task Public_link_options_should_return_only_linked_pairs_in_the_selected_period() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var setup = await GetRequiredService<IAdmissionIntakeSetupAppService>().CreateAsync(ValidSetupInput("Compatible setup"));
			await LinkInputAsync(offering, setup);
			var otherOffering = await CreateOptionalIntakeOfferingAsync();
			await LinkInputAsync(otherOffering, setup);
			var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
			var options = await service.GetLinkOptionsAsync(offering.AdmissionCycleId);
			options.Count.ShouldBe(2);
			options.Single(option => option.CounsellorAssignmentProfileId.HasValue).CounsellorAssignmentProfileId.ShouldBe(setup.CounsellorAssignmentProfileId);
			options.ShouldContain(option => option.CounsellorAssignmentProfileId == null && option.InquiryFormDefinitionId == offering.InquiryFormDefinitionId);
			options.ShouldAllBe(option => option.InquiryFormDefinitionId == offering.InquiryFormDefinitionId!.Value);
			(await service.GetLinkOptionsAsync(Guid.NewGuid())).ShouldBeEmpty();
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(offering.Id);
				stored.Deactivate();
				await _offeringRepository.UpdateAsync(stored, true);
			});
			(await service.GetLinkOptionsAsync(offering.AdmissionCycleId)).ShouldBeEmpty();
		});

	[Theory]
	[InlineData("DirectRegistration")]
	[InlineData("ArchivedSetup")]
	[InlineData("DraftForm")]
	[InlineData("UnscopedUser")]
	public Task Public_link_options_should_exclude_unavailable_or_out_of_scope_pairs(string condition) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var setup = await GetRequiredService<IAdmissionIntakeSetupAppService>().CreateAsync(ValidSetupInput("Options readiness"));
			await LinkInputAsync(offering, setup);
			await WithUnitOfWorkAsync(async () =>
			{
				if (condition == "DirectRegistration")
				{
					var stored = await _offeringRepository.GetAsync(offering.Id);
					stored.UpdateRouting(stored.Track, AdmissionEntryMode.DirectRegistration, false);
					await _offeringRepository.UpdateAsync(stored, true);
				}
				if (condition == "ArchivedSetup")
				{
					var repo = GetRequiredService<IRepository<AdmissionIntakeSetup, Guid>>();
					var stored = await repo.GetAsync(setup.Id);
					stored.Archive(DateTime.UtcNow, TestUserId);
					await repo.UpdateAsync(stored, true);
				}
				if (condition == "DraftForm")
				{
					var draft = new AdmissionFormDefinition(Guid.NewGuid(), TestTenantId, "Draft inquiry", $"DRAFT_{Guid.NewGuid():N}"[..24], AdmissionFormLayoutType.SinglePage, purpose: AdmissionFormPurpose.Inquiry);
					await _formRepository.InsertAsync(draft, true);
					var stored = await _offeringRepository.GetAsync(offering.Id);
					stored.ConfigureReferences(stored.WorkflowDefinitionId, draft.Id, stored.ApplicationFormDefinitionId);
					await _offeringRepository.UpdateAsync(stored, true);
				}
			});
			var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
			if (condition == "UnscopedUser")
			{
				var userId = Guid.NewGuid();
				using (_principalAccessor.Change(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
					new[] { new System.Security.Claims.Claim(Volo.Abp.Security.Claims.AbpClaimTypes.UserId, userId.ToString()) }, "test"))))
					(await service.GetLinkOptionsAsync(offering.AdmissionCycleId)).ShouldBeEmpty();
			}
			else (await service.GetLinkOptionsAsync(offering.AdmissionCycleId)).ShouldBeEmpty();
		});

	private Task SetLinkAssignmentRulesAsync(AdmissionIntakeSetupDto setup, Guid counsellorId, Guid fallbackId) =>
		WithUnitOfWorkAsync(async () =>
		{
			var profiles = GetRequiredService<IRepository<AdmissionIntakeProfile, Guid>>();
			var profile = await profiles.GetAsync(setup.CounsellorAssignmentProfileId);
			profile.Update(profile.Code, profile.Name, JsonSerializer.Serialize(new AdmissionCounsellorAssignmentRules
			{
				CounsellorUserIds = [counsellorId], FallbackManagerUserId = fallbackId
			}));
			await profiles.UpdateAsync(profile, autoSave: true);
		});

	private Task<CreateAdmissionPublicInquiryLinkDto> LinkInputAsync(AdmissionOffering offering, AdmissionIntakeSetupDto setup) => WithUnitOfWorkAsync(async () =>
	{
		var stored = await _offeringRepository.GetAsync(offering.Id);
		stored.ConfigureIntake(setup.InquiryIntakeProfileId, setup.CounsellorAssignmentProfileId);
		await _offeringRepository.UpdateAsync(stored, true);
		return new CreateAdmissionPublicInquiryLinkDto
		{
			AdmissionCycleId = offering.AdmissionCycleId,
			CounsellorAssignmentProfileId = setup.CounsellorAssignmentProfileId,
			InquiryFormDefinitionId = offering.InquiryFormDefinitionId,
			Name = "Readiness test link", Source = AdmissionInquirySource.Website
		};
	});
}
