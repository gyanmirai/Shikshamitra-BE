using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("application")]
	[InlineData("inquiry")]
	public Task Eligibility_offering_rejects_incompatible_create_update_and_activation(string source) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateEligibilityOfferingFixtureAsync(source);
			var service = GetRequiredService<IAdmissionOfferingAppService>();
			var original = await service.GetAsync(fixture.Offering.Id);
			original.Configuration.IsValid.ShouldBeTrue();
			(await service.GetAvailabilityAsync(original.Id)).IsAvailable.ShouldBeTrue();
			var cycleId = await CreateEligibilityOfferingCycleAsync(original.AdmissionCycleId);
			var unrelatedForm = await CreateEligibilityAnswerFormAsync(AdmissionFormFieldType.Number);
			var create = EligibilityOfferingInput<CreateAdmissionOfferingDto>(original);
			SelectEligibilityOfferingForm(create, source, unrelatedForm);
			var createError = await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(cycleId, create));
			createError.Code.ShouldBe(AdmissionEligibilityErrorCodes.OfferingFormMismatch);
			(await WithUnitOfWorkAsync(() => _offeringRepository.CountAsync(item => item.AdmissionCycleId == cycleId))).ShouldBe(0);

			SelectEligibilityOfferingForm(create, source, SelectedEligibilityOfferingForm(original, source));
			var created = await service.CreateAsync(cycleId, create);
			created.Configuration.IsValid.ShouldBeTrue();
			var update = EligibilityOfferingInput<UpdateAdmissionOfferingDto>(created);
			update.ConcurrencyStamp = created.ConcurrencyStamp;
			SelectEligibilityOfferingForm(update, source, unrelatedForm);
			var updateError = await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(created.Id, update));
			updateError.Code.ShouldBe(AdmissionEligibilityErrorCodes.OfferingFormMismatch);
			SelectedEligibilityOfferingForm(await service.GetAsync(created.Id), source)
				.ShouldBe(SelectedEligibilityOfferingForm(original, source));

			var inactive = await service.DeactivateAsync(created.Id, (await service.GetAsync(created.Id)).ConcurrencyStamp);
			await SetStoredEligibilityOfferingFormAsync(inactive.Id, source, unrelatedForm);
			inactive = await service.GetAsync(inactive.Id);
			var activationError = await Should.ThrowAsync<BusinessException>(() => service.ActivateAsync(inactive.Id, inactive.ConcurrencyStamp));
			activationError.Code.ShouldBe(AdmissionEligibilityErrorCodes.OfferingFormMismatch);
			(await service.GetAsync(inactive.Id)).IsActive.ShouldBeFalse();
		});

	[Theory]
	[InlineData("application", AdmissionOfferingValidationCodes.ApplicationFormMissing)]
	[InlineData("inquiry", AdmissionOfferingValidationCodes.InquiryFormMissing)]
	public Task Eligibility_offering_allows_unfinished_draft_but_requires_selected_form_to_activate(string source, string missingCode) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateEligibilityOfferingFixtureAsync(source);
			var service = GetRequiredService<IAdmissionOfferingAppService>();
			var original = await service.GetAsync(fixture.Offering.Id);
			var cycleId = await CreateEligibilityOfferingCycleAsync(original.AdmissionCycleId);
			var create = EligibilityOfferingInput<CreateAdmissionOfferingDto>(original);
			SelectEligibilityOfferingForm(create, source, null);
			var activeError = await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(cycleId, create));
			activeError.Code.ShouldBe(AdmissionErrorCodes.OfferingConfigurationIncomplete);
			create.IsActive = false;
			var draft = await service.CreateAsync(cycleId, create);
			draft.Configuration.Errors.Select(item => item.Code).ShouldContain(missingCode);
			draft.Configuration.Errors.Select(item => item.Code).ShouldNotContain(AdmissionOfferingValidationCodes.EligibilityFormMismatch);
			var activationError = await Should.ThrowAsync<BusinessException>(() => service.ActivateAsync(draft.Id, draft.ConcurrencyStamp));
			activationError.Code.ShouldBe(AdmissionErrorCodes.OfferingConfigurationIncomplete);

			var update = EligibilityOfferingInput<UpdateAdmissionOfferingDto>(await service.GetAsync(draft.Id));
			update.ConcurrencyStamp = (await service.GetAsync(draft.Id)).ConcurrencyStamp;
			SelectEligibilityOfferingForm(update, source, SelectedEligibilityOfferingForm(original, source));
			draft = await service.UpdateAsync(draft.Id, update);
			var activated = await service.ActivateAsync(draft.Id, draft.ConcurrencyStamp);
			activated.IsActive.ShouldBeTrue();
			activated.Configuration.IsValid.ShouldBeTrue();
			(await service.GetAvailabilityAsync(draft.Id)).CanStartApplication.ShouldBeTrue();
		});

	[Theory]
	[InlineData("application")]
	[InlineData("inquiry")]
	public Task Eligibility_offering_existing_mismatch_is_incomplete_in_detail_filters_and_runtime(string source) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateEligibilityOfferingFixtureAsync(source);
			var service = GetRequiredService<IAdmissionOfferingAppService>();
			var unrelatedForm = await CreateEligibilityAnswerFormAsync(AdmissionFormFieldType.Number);
			// Simulate a persisted selection made before the compatibility guard existed.
			await SetStoredEligibilityOfferingFormAsync(fixture.Offering.Id, source, unrelatedForm);
			var detail = await service.GetAsync(fixture.Offering.Id);
			detail.ConfigurationStatus.ShouldBe(AdmissionOfferingConfigurationStatus.Incomplete);
			detail.Configuration.Errors.Select(item => item.Code).ShouldContain(AdmissionOfferingValidationCodes.EligibilityFormMismatch);
			var validation = await WithUnitOfWorkAsync(async () =>
				await _offeringValidator.ValidateStoredAsync(await _offeringRepository.GetAsync(detail.Id)));
			validation.Errors.Select(item => item.Code).ShouldContain(AdmissionOfferingValidationCodes.EligibilityFormMismatch);
			var availability = await service.GetAvailabilityAsync(detail.Id);
			availability.IsAvailable.ShouldBeFalse();
			availability.CanStartApplication.ShouldBeFalse();
			availability.CanCreateInquiry.ShouldBeFalse();
			availability.Status.ShouldBe(AdmissionOfferingAvailabilityStatus.ConfigurationIncomplete);
			var ready = await service.GetListAsync(new() { AdmissionCycleId = detail.AdmissionCycleId,
				ConfigurationStatus = AdmissionOfferingConfigurationStatus.Ready });
			var open = await service.GetListAsync(new() { AdmissionCycleId = detail.AdmissionCycleId,
				AvailabilityStatus = AdmissionOfferingAvailabilityStatus.Open });
			ready.Items.ShouldBeEmpty();
			open.Items.ShouldBeEmpty();
			var incomplete = await service.GetListAsync(new() { AdmissionCycleId = detail.AdmissionCycleId,
				ConfigurationStatus = AdmissionOfferingConfigurationStatus.Incomplete });
			incomplete.Items.Single().Id.ShouldBe(detail.Id);
			var applicant = await CreateApplicantAsync("Compatibility");
			await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartAsync(applicant.Id, detail.Id));
			(await WithUnitOfWorkAsync(() => _applicationRepository.CountAsync(item => item.ApplicantId == applicant.Id))).ShouldBe(0);
		});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Eligibility_offering_legacy_and_disabled_policies_preserve_unrelated_published_forms(bool disabled) =>
		RunCertificationScenarioAsync(async () =>
		{
			var referencedForm = await CreateEligibilityAnswerFormAsync(AdmissionFormFieldType.Number);
			var policy = disabled
				? EligibilityPolicyJson([EligibilityOfferingCriterion("application", referencedForm)])
					.Replace("\"enabled\":true", "\"enabled\":false", StringComparison.Ordinal)
				: "{}";
			var fixture = await CreateEligibilityFixtureAsync(policy, new DateTime(2021, 4, 1));
			var service = GetRequiredService<IAdmissionOfferingAppService>();
			var original = await service.GetAsync(fixture.Offering.Id);
			original.ApplicationFormDefinitionId.ShouldNotBe(referencedForm);
			var cycleId = await CreateEligibilityOfferingCycleAsync(original.AdmissionCycleId);
			var created = await service.CreateAsync(cycleId, EligibilityOfferingInput<CreateAdmissionOfferingDto>(original));
			created.Configuration.IsValid.ShouldBeTrue();
			(await service.GetAvailabilityAsync(created.Id)).IsAvailable.ShouldBeTrue();
			var open = await service.GetListAsync(new() { AdmissionCycleId = cycleId,
				AvailabilityStatus = AdmissionOfferingAvailabilityStatus.Open });
			open.Items.Single().Id.ShouldBe(created.Id);
		});

	private async Task<EligibilityFixture> CreateEligibilityOfferingFixtureAsync(string source)
	{
		var formId = await CreateEligibilityAnswerFormAsync(AdmissionFormFieldType.Number);
		return await CreateEligibilityFixtureAsync(EligibilityPolicyJson([EligibilityOfferingCriterion(source, formId)]),
			new DateTime(2021, 4, 1), applicationFormDefinitionId: source == "application" ? formId : null,
			inquiryFormDefinitionId: source == "inquiry" ? formId : null);
	}

	private static object EligibilityOfferingCriterion(string source, Guid formId) => new
	{
		code = "SOURCE_MARKS", name = "Previous school marks", kind = source, formDefinitionId = formId,
		conditionJson = "{\"field\":\"" + source + ".ANSWER\",\"operator\":\"GreaterThanOrEqual\",\"value\":50}"
	};

	private Task<Guid> CreateEligibilityOfferingCycleAsync(Guid sourceCycleId) => WithUnitOfWorkAsync(async () =>
	{
		var source = await _cycleRepository.GetAsync(sourceCycleId);
		var cycle = new AdmissionCycle(Guid.NewGuid(), TestTenantId, "Eligibility compatibility cycle",
			$"ELIG_{Guid.NewGuid():N}"[..24], source.AcademicYearId, source.StartDate, source.EndDate);
		cycle.Publish(TestUserId, DateTime.UtcNow);
		await _cycleRepository.InsertAsync(cycle, autoSave: true);
		return cycle.Id;
	});

	private Task SetStoredEligibilityOfferingFormAsync(Guid offeringId, string source, Guid formId) => WithUnitOfWorkAsync(async () =>
	{
		var offering = await _offeringRepository.GetAsync(offeringId);
		offering.ConfigureReferences(offering.WorkflowDefinitionId,
			source == "inquiry" ? formId : offering.InquiryFormDefinitionId,
			source == "application" ? formId : offering.ApplicationFormDefinitionId);
		await _offeringRepository.UpdateAsync(offering, autoSave: true);
	});

	private static T EligibilityOfferingInput<T>(AdmissionOfferingDto source) where T : AdmissionOfferingWriteDto, new() => new()
	{
		CampusId = source.CampusId, BoardId = source.BoardId, MediumId = source.MediumId,
		StageId = source.StageId, StandardId = source.StandardId, Code = source.Code,
		UseCycleDates = source.UseCycleDates, OpenDate = source.OpenDate, CloseDate = source.CloseDate,
		WorkflowDefinitionId = source.WorkflowDefinitionId, InquiryFormDefinitionId = source.InquiryFormDefinitionId,
		ApplicationFormDefinitionId = source.ApplicationFormDefinitionId, IsActive = source.IsActive
	};

	private static Guid? SelectedEligibilityOfferingForm(AdmissionOfferingDto offering, string source) =>
		source == "application" ? offering.ApplicationFormDefinitionId : offering.InquiryFormDefinitionId;

	private static void SelectEligibilityOfferingForm(AdmissionOfferingWriteDto offering, string source, Guid? formId)
	{
		if (source == "application") offering.ApplicationFormDefinitionId = formId;
		else offering.InquiryFormDefinitionId = formId;
	}
}
