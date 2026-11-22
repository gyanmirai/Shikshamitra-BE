using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(-1)]
	[InlineData(0)]
	public Task Public_link_should_explain_expiry_at_or_before_now(int minutesFromNow) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var input = FormLinkInput(offering);
		input.ExpiresAtUtc = GetRequiredService<IClock>().Now.ToUniversalTime().AddMinutes(minutesFromNow);
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IAdmissionPublicInquiryAppService>().CreateLinkAsync(input)))
			.Code.ShouldBe(AdmissionErrorCodes.PublicInquiryExpiryInvalid);
		(await GetRequiredService<IRepository<AdmissionPublicInquiryLink, Guid>>().GetListAsync()).ShouldBeEmpty();
	});

	[Theory]
	[InlineData(false, AdmissionEntryMode.Inquiry)]
	[InlineData(true, AdmissionEntryMode.DirectRegistration)]
	public Task Public_link_should_explain_missing_active_inquiry_offering(bool active, AdmissionEntryMode entryMode) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var stored = await _offeringRepository.GetAsync(offering.Id);
		stored.UpdateRouting(stored.Track, entryMode, false);
		if (!active) stored.Deactivate();
		await _offeringRepository.UpdateAsync(stored, true);
		var error = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IAdmissionPublicInquiryAppService>().CreateLinkAsync(FormLinkInput(offering)));
		error.Code.ShouldBe(AdmissionErrorCodes.PublicInquiryOfferingRequired);
		error.Data["PeriodName"].ShouldBe((await _cycleRepository.GetAsync(offering.AdmissionCycleId)).Name);
		error.Message.ShouldContain("accessible active Offering");
		(await GetRequiredService<IRepository<AdmissionPublicInquiryLink, Guid>>().GetListAsync()).ShouldBeEmpty();
	});

	[Fact]
	public Task Public_link_create_should_reject_duplicate_name_across_periods_and_preserve_existing_link() => RunCertificationScenarioAsync(async () =>
	{
		var firstOffering = await CreateOptionalIntakeOfferingAsync();
		var secondOffering = await CreateOptionalIntakeOfferingAsync();
		var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
		var firstInput = FormLinkInput(firstOffering);
		firstInput.Name = "  Link 1  ";
		var saved = await service.CreateLinkAsync(firstInput);
		saved.Name.ShouldBe("Link 1");
		var duplicateInput = FormLinkInput(secondOffering);
		duplicateInput.Name = " link 1 ";
		var error = await Should.ThrowAsync<BusinessException>(() => service.CreateLinkAsync(duplicateInput));
		error.Code.ShouldBe(AdmissionErrorCodes.PublicInquiryLinkNameExists);
		(await service.GetLinksAsync()).ShouldHaveSingleItem().Id.ShouldBe(saved.Id);
	});

	[Fact]
	public Task Public_link_rename_should_reject_a_reserved_name_and_preserve_link_state() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
		var input = FormLinkInput(offering);
		input.Name = "Original";
		input.ExpiresAtUtc = GetRequiredService<IClock>().Now.ToUniversalTime().AddDays(1);
		var original = await service.CreateLinkAsync(input);
		input.Name = "Reserved";
		var reserved = await service.CreateLinkAsync(input);
		await service.RevokeLinkAsync(reserved.Id, reserved.ConcurrencyStamp);
		var repository = GetRequiredService<IRepository<AdmissionPublicInquiryLink, Guid>>();
		var originalTokenHash = (await repository.GetAsync(original.Id)).TokenHash;

		var rename = new RenameAdmissionPublicInquiryLinkDto { Name = " reserved ", ConcurrencyStamp = original.ConcurrencyStamp };
		var duplicate = await Should.ThrowAsync<BusinessException>(() => service.RenameLinkAsync(original.Id, rename));
		duplicate.Code.ShouldBe(AdmissionErrorCodes.PublicInquiryLinkNameExists);
		(await repository.GetAsync(original.Id)).Name.ShouldBe("Original");

		rename.Name = " original ";
		var renamed = await service.RenameLinkAsync(original.Id, rename);
		renamed.Name.ShouldBe("original");
		renamed.AdmissionCycleId.ShouldBe(original.AdmissionCycleId);
		renamed.InquiryFormDefinitionId.ShouldBe(original.InquiryFormDefinitionId);
		renamed.Source.ShouldBe(original.Source);
		renamed.ExpiresAtUtc.ShouldBe(original.ExpiresAtUtc);
		renamed.IsActive.ShouldBeTrue();
		(await repository.GetAsync(original.Id)).TokenHash.ShouldBe(originalTokenHash);
		var stale = await Should.ThrowAsync<BusinessException>(() => service.RenameLinkAsync(original.Id, rename));
		stale.Code.ShouldBe(AdmissionIntakeProfileErrorCodes.ConcurrencyConflict);
	});

	[Fact]
	public Task Public_link_names_should_be_independent_across_tenants() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var otherTenantId = Guid.NewGuid();
		var repository = GetRequiredService<IRepository<AdmissionPublicInquiryLink, Guid>>();
		using (_currentTenant.Change(otherTenantId))
		{
			await repository.InsertAsync(new AdmissionPublicInquiryLink(Guid.NewGuid(), otherTenantId,
				offering.AdmissionCycleId, null, "FORM READINESS TEST", "other-tenant-hash", AdmissionInquirySource.Website,
				null, null, offering.InquiryFormDefinitionId), autoSave: true);
		}
		(await GetRequiredService<IAdmissionPublicInquiryAppService>().CreateLinkAsync(FormLinkInput(offering))).Name
			.ShouldBe("Form readiness test");
	});

	[Fact]
	public Task Public_link_should_ignore_retired_pool_and_fallback_and_preserve_form_matching() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var input = FormLinkInput(offering);
		input.CounsellorAssignmentProfileId = Guid.NewGuid();
		var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
		var link = await service.CreateLinkAsync(input);
		link.CounsellorAssignmentProfileId.ShouldBeNull();
		link.InquiryFormDefinitionId.ShouldBe(offering.InquiryFormDefinitionId);
		var originalForm = input.InquiryFormDefinitionId;
		input.InquiryFormDefinitionId = (await CreateOptionalIntakeOfferingAsync()).InquiryFormDefinitionId;
		(await Should.ThrowAsync<BusinessException>(() => service.CreateLinkAsync(input))).Code.ShouldBe(AdmissionErrorCodes.PublicInquiryOfferingRequired);
		input.InquiryFormDefinitionId = originalForm;
		input.Name = "Second form readiness test";
		(await service.CreateLinkAsync(input)).IsActive.ShouldBeTrue();
	});

	[Fact]
	public Task Public_link_options_should_return_only_linked_forms_in_the_selected_period() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		await CreateOptionalIntakeOfferingAsync();
		var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
		var option = (await service.GetLinkOptionsAsync(offering.AdmissionCycleId)).ShouldHaveSingleItem();
		option.CounsellorAssignmentProfileId.ShouldBeNull();
		option.InquiryFormDefinitionId.ShouldBe(offering.InquiryFormDefinitionId!.Value);
		(await service.GetLinkOptionsAsync(Guid.NewGuid())).ShouldBeEmpty();
	});

	[Theory]
	[InlineData("DirectRegistration")]
	[InlineData("DraftForm")]
	[InlineData("UnscopedUser")]
	public Task Public_link_options_should_exclude_unavailable_or_out_of_scope_forms(string condition) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var stored = await _offeringRepository.GetAsync(offering.Id);
		if (condition == "DirectRegistration") stored.UpdateRouting(stored.Track, AdmissionEntryMode.DirectRegistration, false);
		if (condition == "DraftForm")
		{
			var draft = new AdmissionFormDefinition(Guid.NewGuid(), TestTenantId, "Draft inquiry", $"DRAFT_{Guid.NewGuid():N}"[..24], AdmissionFormLayoutType.SinglePage, purpose: AdmissionFormPurpose.Inquiry);
			await _formRepository.InsertAsync(draft, true);
			stored.ConfigureReferences(stored.WorkflowDefinitionId, draft.Id, stored.ApplicationFormDefinitionId);
		}
		await _offeringRepository.UpdateAsync(stored, true);
		var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
		if (condition == "UnscopedUser")
		{
			using var principal = _principalAccessor.Change(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
				[new System.Security.Claims.Claim(Volo.Abp.Security.Claims.AbpClaimTypes.UserId, Guid.NewGuid().ToString())], "test")));
			(await service.GetLinkOptionsAsync(offering.AdmissionCycleId)).ShouldBeEmpty();
		}
		else (await service.GetLinkOptionsAsync(offering.AdmissionCycleId)).ShouldBeEmpty();
	});

	private static CreateAdmissionPublicInquiryLinkDto FormLinkInput(AdmissionOffering offering) => new()
	{
		AdmissionCycleId = offering.AdmissionCycleId, InquiryFormDefinitionId = offering.InquiryFormDefinitionId,
		Name = "Form readiness test", Source = AdmissionInquirySource.Website
	};

	private Task<CreateAdmissionPublicInquiryLinkDto> LinkInputAsync(AdmissionOffering offering, AdmissionIntakeSetupDto setup) => WithUnitOfWorkAsync(async () =>
	{
		var stored = await _offeringRepository.GetAsync(offering.Id);
		stored.ConfigureIntake(setup.InquiryIntakeProfileId, setup.CounsellorAssignmentProfileId);
		await _offeringRepository.UpdateAsync(stored, true);
		return FormLinkInput(offering);
	});
}
