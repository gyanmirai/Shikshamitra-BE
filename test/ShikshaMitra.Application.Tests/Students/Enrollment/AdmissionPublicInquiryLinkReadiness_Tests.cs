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
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			var setup = await GetRequiredService<IAdmissionIntakeSetupAppService>()
				.CreateAsync(ValidSetupInput("Expiry readiness"));
			var input = LinkInput(offering, setup);
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
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			var setup = await GetRequiredService<IAdmissionIntakeSetupAppService>()
				.CreateAsync(ValidSetupInput("Link readiness"));
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(offering.Id);
				stored.UpdateRouting(stored.Track, entryMode, identityLocked: false);
				if (!active) stored.Deactivate();
				await _offeringRepository.UpdateAsync(stored, autoSave: true);
			});
			var error = await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<IAdmissionPublicInquiryAppService>().CreateLinkAsync(LinkInput(offering, setup)));
			error.Code.ShouldBe(AdmissionErrorCodes.PublicInquiryOfferingRequired);
			error.Data["PeriodName"].ShouldBe((await _cycleRepository.GetAsync(offering.AdmissionCycleId)).Name);
			(await GetRequiredService<IRepository<AdmissionPublicInquiryLink, Guid>>()
				.GetListAsync(item => item.AdmissionCycleId == offering.AdmissionCycleId)).ShouldBeEmpty();
		});

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public Task Public_link_should_explain_inactive_or_unscoped_counsellor(bool active) =>
		RunCertificationScenarioAsync(async () =>
		{
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
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
			var error = await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<IAdmissionPublicInquiryAppService>().CreateLinkAsync(LinkInput(offering, setup)));
			error.Code.ShouldBe(AdmissionErrorCodes.PublicInquiryCounsellorUnavailable);
			error.Data["SetupName"].ShouldBe(setup.Name);
		});

	[Fact]
	public Task Public_link_should_explain_unscoped_fallback_even_when_pool_is_eligible() =>
		RunCertificationScenarioAsync(async () =>
		{
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			var setup = await GetRequiredService<IAdmissionIntakeSetupAppService>()
				.CreateAsync(ValidSetupInput("Fallback readiness"));
			var fallbackId = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(fallbackId, $"link-fallback-{fallbackId:N}");
			await SetLinkAssignmentRulesAsync(setup, TestUserId, fallbackId);
			var error = await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<IAdmissionPublicInquiryAppService>().CreateLinkAsync(LinkInput(offering, setup)));
			error.Code.ShouldBe(AdmissionErrorCodes.PublicInquiryFallbackUnavailable);
			error.Data["SetupName"].ShouldBe(setup.Name);
		});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Public_link_should_still_create_with_eligible_pool_and_fallback(bool withExpiry) =>
		RunCertificationScenarioAsync(async () =>
		{
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			var setup = await GetRequiredService<IAdmissionIntakeSetupAppService>()
				.CreateAsync(ValidSetupInput("Ready intake"));
			var input = LinkInput(offering, setup);
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

	private static CreateAdmissionPublicInquiryLinkDto LinkInput(AdmissionOffering offering, AdmissionIntakeSetupDto setup) => new()
	{
		AdmissionCycleId = offering.AdmissionCycleId,
		CounsellorAssignmentProfileId = setup.CounsellorAssignmentProfileId,
		InquiryFormDefinitionId = offering.InquiryFormDefinitionId,
		Name = "Readiness test link", Source = AdmissionInquirySource.Website
	};
}
