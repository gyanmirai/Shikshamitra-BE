using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicSetups;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Offering_simplification_uses_workflow_form_and_preserves_legacy_references() => RunCertificationScenarioAsync(async () =>
	{
		var original = await CreateOptionalIntakeOfferingAsync();
		await ConfigureOfferingWorkflowFormAsync(original);
		var cycleId = await CreateEligibilityOfferingCycleAsync(original.AdmissionCycleId);
		var input = await EligibilityOfferingInputAsync<CreateAdmissionOfferingDto>(await GetRequiredService<IAdmissionOfferingAppService>().GetAsync(original.Id));
		input.ApplicationFormDefinitionId = Guid.NewGuid();
		input.Track = AdmissionTrack.Management;
		input.EntryMode = AdmissionEntryMode.Inquiry;
		input.IsActive = false;
		var created = await GetRequiredService<IAdmissionOfferingAppService>().CreateAsync(cycleId, input);
		created.ApplicationFormDefinitionId.ShouldBe(original.ApplicationFormDefinitionId);
		created.InquiryFormDefinitionId.ShouldBe(original.InquiryFormDefinitionId);
		created.Track.ShouldBe(AdmissionTrack.Management);
		created.EntryMode.ShouldBe(AdmissionEntryMode.Inquiry);
		created.Configuration.Errors.ShouldNotContain(error => error.Code == AdmissionOfferingValidationCodes.WorkflowFormMismatch);

		var legacy = await CreateOptionalIntakeOfferingAsync();
		var update = await EligibilityOfferingInputAsync<UpdateAdmissionOfferingDto>(await GetRequiredService<IAdmissionOfferingAppService>().GetAsync(legacy.Id));
		update.ApplicationFormDefinitionId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await _offeringValidator.ValidateAsync(legacy.AdmissionCycleId, update, legacy.Id);
			update.ApplicationFormDefinitionId.ShouldBe(legacy.ApplicationFormDefinitionId);
		});
	});

	[Fact]
	public Task Offering_simplification_preserves_unused_special_offering_when_edited() => RunCertificationScenarioAsync(async () =>
	{
		var legacy = await CreateSpecialRegistrationOfferingAsync();
		var service = GetRequiredService<IAdmissionOfferingAppService>();
		var input = await EligibilityOfferingInputAsync<UpdateAdmissionOfferingDto>(await service.GetAsync(legacy.Id));
		input.Track = legacy.Track;
		input.EntryMode = legacy.EntryMode;
		input.ConcurrencyStamp = (await service.GetAsync(legacy.Id)).ConcurrencyStamp;
		var updated = await service.UpdateAsync(legacy.Id, input);
		updated.Track.ShouldBe(AdmissionTrack.Rte);
		updated.EntryMode.ShouldBe(AdmissionEntryMode.DirectRegistration);
		updated.ApplicationFormDefinitionId.ShouldBe(legacy.ApplicationFormDefinitionId);
	});

	[Fact]
	public Task Offering_simplification_reports_missing_and_conflicting_workflow_forms() => RunCertificationScenarioAsync(async () =>
	{
		var original = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var cycleId = await CreateEligibilityOfferingCycleAsync(original.AdmissionCycleId);
		var input = await EligibilityOfferingInputAsync<CreateAdmissionOfferingDto>(await GetRequiredService<IAdmissionOfferingAppService>().GetAsync(original.Id));
		input.IsActive = false;
		var created = await GetRequiredService<IAdmissionOfferingAppService>().CreateAsync(cycleId, input);
		created.ApplicationFormDefinitionId.ShouldBeNull();
		created.Configuration.Errors.ShouldContain(error => error.Code == AdmissionOfferingValidationCodes.ApplicationFormMissing);
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IAdmissionOfferingAppService>().ActivateAsync(created.Id, created.ConcurrencyStamp)))
			.Code.ShouldBe(AdmissionErrorCodes.OfferingConfigurationIncomplete);
		await ConfigureOfferingWorkflowFormAsync(original);
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>().InsertAsync(new(
				Guid.NewGuid(), original.WorkflowDefinitionId!.Value, "SECOND_FORM", "Other form",
				AdmissionWorkflowStageType.Form, 8, true, false, null,
				JsonSerializer.Serialize(new { formDefinitionId = Guid.NewGuid() })), autoSave: true);
			var result = await _offeringValidator.ValidateAsync(cycleId, input);
			result.Errors.ShouldContain(error => error.Code == AdmissionOfferingValidationCodes.WorkflowFormMismatch);
			AdmissionOfferingConfigurationValidator.CanSave(result, false).ShouldBeFalse();
		});
	});

	[Fact]
	public Task Offering_simplification_bulk_derives_each_class_stage_without_trusting_filter() => RunCertificationScenarioAsync(async () =>
	{
		var original = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		await ConfigureOfferingWorkflowFormAsync(original);
		var otherClass = await WithUnitOfWorkAsync(async () =>
		{
			var stage = await GetRequiredService<EducationMediumStageManager>().CreateAsync(original.MediumId, $"Secondary-{Guid.NewGuid():N}"[..30]);
			await _stageRepository.InsertAsync(stage, autoSave: true);
			var standard = await _standardManager.CreateAsync(stage.Id, "Class 9", 14, null);
			await _standardRepository.InsertAsync(standard, autoSave: true);
			return standard;
		});
		var cycleId = await CreateEligibilityOfferingCycleAsync(original.AdmissionCycleId);
		var input = new BulkCreateAdmissionOfferingsDto
		{
			BoardId = original.BoardId, MediumId = original.MediumId, StageId = Guid.NewGuid(),
			WorkflowDefinitionId = original.WorkflowDefinitionId, InquiryFormDefinitionId = original.InquiryFormDefinitionId,
			UseCycleDates = true, IsActive = false, PreviewOnly = false,
			Items = [new() { StandardId = original.StandardId, Code = "ADM-CLASS-5" }, new() { StandardId = otherClass.Id, Code = "ADM-CLASS-9" }]
		};
		var result = await GetRequiredService<IAdmissionOfferingAppService>().BulkCreateAsync(cycleId, input);
		result.CreatedCount.ShouldBe(2);
		var created = await WithUnitOfWorkAsync(() => _offeringRepository.GetListAsync(item => item.AdmissionCycleId == cycleId));
		created.Single(item => item.StandardId == original.StandardId).StageId.ShouldBe(original.StageId);
		created.Single(item => item.StandardId == otherClass.Id).StageId.ShouldBe(otherClass.EducationMediumStageId);
		created.ShouldAllBe(item => item.ApplicationFormDefinitionId == original.ApplicationFormDefinitionId);
	});

	[Fact]
	public Task Offering_simplification_blocks_generic_fresh_application_without_inquiry() => RunCertificationScenarioAsync(async () =>
	{
		var original = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var applicant = await CreateApplicantAsync("Fresh direct");
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartAsync(applicant.Id, original.Id)))
			.Code.ShouldBe(AdmissionErrorCodes.DirectRegistrationEntryNotAllowed);
		(await WithUnitOfWorkAsync(() => _applicationRepository.CountAsync(item => item.ApplicantId == applicant.Id))).ShouldBe(0);
	});

	private Task ConfigureOfferingWorkflowFormAsync(AdmissionOffering offering) => WithUnitOfWorkAsync(async () =>
	{
		var repository = GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>();
		var stage = (await repository.GetListAsync(item => item.WorkflowDefinitionId == offering.WorkflowDefinitionId && item.StageType == AdmissionWorkflowStageType.Form)).FirstOrDefault();
		if (stage == null)
			await repository.InsertAsync(new(Guid.NewGuid(), offering.WorkflowDefinitionId!.Value,
				"REGISTRATION", "Registration", AdmissionWorkflowStageType.Form, 3, true, false, null,
				JsonSerializer.Serialize(new { formDefinitionId = offering.ApplicationFormDefinitionId })), autoSave: true);
		else
		{
			stage.Update(stage.Code, stage.Name, stage.StageType, stage.DisplayOrder, stage.IsRequired,
				stage.AllowManualSkip, stage.Description, JsonSerializer.Serialize(new { formDefinitionId = offering.ApplicationFormDefinitionId }), stage.IsEntryStage, stage.IsTerminalStage);
			await repository.UpdateAsync(stage, autoSave: true);
		}
	});
}
