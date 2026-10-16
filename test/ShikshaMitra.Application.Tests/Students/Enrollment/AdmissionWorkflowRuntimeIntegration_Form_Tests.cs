using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Legacy_Inquiry_Then_Form_Should_Advance_Past_Both_Stages_On_Submit()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			var workflowId = await CreateLegacyInquiryFormWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflowId, assertValid: false);
			var applicant = await CreateApplicantAsync("Kavya");
			var definitionRepository = GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>();
			var workflowInstanceRepository = GetRequiredService<IRepository<AdmissionApplicationWorkflowInstance, Guid>>();
			var stageInstanceRepository = GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>();
			var workflowService = GetRequiredService<AdmissionApplicationWorkflowService>();

			var applicationId = await WithUnitOfWorkAsync(async () =>
			{
				var definitions = (await definitionRepository.GetListAsync(stage =>
					stage.WorkflowDefinitionId == workflowId)).OrderBy(stage => stage.DisplayOrder).ToList();
				var application = new AdmissionApplication(
					Guid.NewGuid(), TestTenantId, $"APP-2026-{Guid.NewGuid():N}"[..19], applicant.Id, null,
					offering.AdmissionCycleId, offering.Id, workflowId,
					offering.ApplicationFormDefinitionId!.Value);
				var workflowInstance = new AdmissionApplicationWorkflowInstance(
					Guid.NewGuid(), TestTenantId, application.Id, workflowId);
				var stages = definitions.Select((definition, index) => new AdmissionApplicationStageInstance(
					Guid.NewGuid(), TestTenantId, workflowInstance.Id, definition.Id, definition.Code,
					definition.Name, definition.StageType, definition.DisplayOrder, index == 0)).ToList();
				application.SetCurrentStage(stages[0].Id);
				application.Submit();
				await _applicationRepository.InsertAsync(application, false);
				await workflowInstanceRepository.InsertAsync(workflowInstance, false);
				foreach (var stage in stages) await stageInstanceRepository.InsertAsync(stage, false);
				await workflowInstanceRepository.UpdateAsync(workflowInstance, true);

				await workflowService.AdvanceAfterSubmitAsync(application);
				return application.Id;
			});

			await WithUnitOfWorkAsync(async () =>
			{
				var application = await _applicationRepository.GetAsync(applicationId);
				var workflowInstance = await workflowInstanceRepository.GetAsync(instance =>
					instance.ApplicationId == applicationId);
				var stages = await stageInstanceRepository.GetListAsync(stage =>
					stage.WorkflowInstanceId == workflowInstance.Id);
				stages.Single(stage => stage.StageType == AdmissionWorkflowStageType.Inquiry).Status
					.ShouldBe(AdmissionRuntimeStageStatus.Completed);
				stages.Single(stage => stage.StageType == AdmissionWorkflowStageType.Form).Status
					.ShouldBe(AdmissionRuntimeStageStatus.Completed);
				stages.Single(stage => stage.Id == application.CurrentStageInstanceId).Code
					.ShouldBe("DOCUMENTS");
			});
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Managed_Form_Should_Hold_The_Workflow_For_Verification_And_Audit_Corrections(bool endsAtForm)
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new Security.UserScope.UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Managed Form integration test"), true);
			var basicWorkflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(basicWorkflow.Id);
			var managedWorkflow = await CreateManagedFormWorkflowAsync(offering.ApplicationFormDefinitionId!.Value, endsAtForm);
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(offering.Id);
				stored.ConfigureReferences(managedWorkflow.Id, stored.InquiryFormDefinitionId, stored.ApplicationFormDefinitionId);
				await _offeringRepository.UpdateAsync(stored, true);
			});

			var application = await StartFixtureApplicationAsync((await CreateApplicantAsync("Mira")).Id, offering.Id);
			var workflow = await _applicationAppService.GetWorkflowAsync(application.Id);
			var stage = workflow.Stages.Single(item => item.StageType == AdmissionWorkflowStageType.Form);
			stage.FormStageEnabled.ShouldBeTrue();
			var service = GetRequiredService<IAdmissionFormStageAppService>();
			var initial = await service.GetAsync(application.Id, stage.Id);
			initial.CanSubmit.ShouldBeTrue();
			initial.Blockers.ShouldContain("Submit the application form.");

			(await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
			workflow = await _applicationAppService.GetWorkflowAsync(application.Id);
			workflow.CurrentStageInstanceId.ShouldBe(stage.Id);
			var submitted = await service.GetAsync(application.Id, stage.Id);
			submitted.SubmissionAttempts.ShouldBe(1);
			submitted.CanComplete.ShouldBeFalse();
			var section = submitted.Sections.Single(item => item.Required);

			var verified = await service.VerifySectionAsync(application.Id, stage.Id, section.SectionId,
				new VerifyAdmissionFormSectionDto { ConcurrencyStamp = submitted.ConcurrencyStamp, Verified = true });
			verified.CanComplete.ShouldBeTrue();
			verified.Sections.Single(item => item.Required).Verified.ShouldBeTrue();

			var corrected = await service.RequestCorrectionAsync(application.Id, stage.Id,
				new RequestAdmissionFormCorrectionDto
				{
					ConcurrencyStamp = verified.ConcurrencyStamp,
					Reason = "Confirm the guardian contact before final review."
				});
			corrected.FormStatus.ShouldBe(AdmissionFormInstanceStatus.Reopened);
			corrected.Sections.Single(item => item.Required).Verified.ShouldBeFalse();
			(await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
			var resubmitted = await service.GetAsync(application.Id, stage.Id);
			resubmitted.SubmissionAttempts.ShouldBe(2);
			var ready = await service.VerifySectionAsync(application.Id, stage.Id, section.SectionId,
				new VerifyAdmissionFormSectionDto { ConcurrencyStamp = resubmitted.ConcurrencyStamp, Verified = true });

			workflow = await _applicationAppService.AdvanceWorkflowAsync(application.Id,
				new AdvanceAdmissionWorkflowDto
				{
					ExpectedCurrentStageInstanceId = stage.Id,
					IdempotencyKey = "managed-form-complete",
					ConcurrencyStamp = ready.ConcurrencyStamp
				});
			if (endsAtForm)
			{
				workflow.Status.ShouldBe(AdmissionWorkflowInstanceStatus.Completed);
				workflow.CompletedAt.ShouldNotBeNull();
				workflow.Stages.Single().Status.ShouldBe(AdmissionRuntimeStageStatus.Completed);
				workflow.NextRequiredAction.ShouldBeNull();
			}
			else
			{
				workflow.Stages.Single(item => item.Code == "DONE").Status.ShouldBe(AdmissionRuntimeStageStatus.Active);
			}
			var history = await _applicationAppService.GetFormHistoryAsync(application.Id);
			history.ShouldContain(item => item.ChangeType == "CorrectionRequested");
			history.Count(item => item.ChangeType == "SectionVerification").ShouldBe(2);
		}
	}

	private async Task<Guid> CreateLegacyInquiryFormWorkflowAsync()
	{
		var workflowId = Guid.NewGuid();
		var inquiryStageId = Guid.NewGuid();
		var formStageId = Guid.NewGuid();
		var documentsStageId = Guid.NewGuid();
		var workflowRepository = GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>();
		var stageRepository = GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>();
		var transitionRepository = GetRequiredService<IRepository<AdmissionWorkflowTransitionDefinition, Guid>>();
		var workflow = new AdmissionWorkflowDefinition(
			workflowId, TestTenantId, "Legacy inquiry and application form",
			$"LEGACY_FORM_{Guid.NewGuid():N}"[..24]);
		workflow.Publish(TestUserId);
		await workflowRepository.InsertAsync(workflow, false);
		await stageRepository.InsertAsync(new AdmissionWorkflowStageDefinition(
			inquiryStageId, workflowId, "INQUIRY", "Basic inquiry received",
			AdmissionWorkflowStageType.Inquiry, 0, true, false, null, null, true), false);
		await stageRepository.InsertAsync(new AdmissionWorkflowStageDefinition(
			formStageId, workflowId, "FORM", "Application form submitted",
			AdmissionWorkflowStageType.Form, 1, true, false, null, null), false);
		await stageRepository.InsertAsync(new AdmissionWorkflowStageDefinition(
			documentsStageId, workflowId, "DOCUMENTS", "Documents verified",
			AdmissionWorkflowStageType.Custom, 2, true, false, null, null,
			isTerminalStage: true), false);
		await transitionRepository.InsertAsync(new AdmissionWorkflowTransitionDefinition(
			Guid.NewGuid(), workflowId, inquiryStageId, formStageId, 1, isDefault: true), false);
		await transitionRepository.InsertAsync(new AdmissionWorkflowTransitionDefinition(
			Guid.NewGuid(), workflowId, formStageId, documentsStageId, 1, isDefault: true), true);
		return workflowId;
	}

	private async Task<AdmissionWorkflowDto> CreateManagedFormWorkflowAsync(Guid formId, bool endsAtForm = false)
	{
		var formStageId = Guid.NewGuid();
		var doneStageId = Guid.NewGuid();
		var policy = new AdmissionFormStagePolicy
		{
			Version = 1,
			Enabled = true,
			Responsibility = "shared",
			AllowSaveAndResume = true,
			AllowLateSubmission = true,
			AutoAdvanceOnSubmit = false,
			AllowCorrectionRequests = true,
			MaxSubmissionAttempts = 3,
			RequireSectionVerification = true,
			VerificationSectionCodes = ["STUDENT"]
		};
		var input = new CreateUpdateAdmissionWorkflowDto
		{
			Name = "Managed application form",
			Code = $"FORM_{Guid.NewGuid():N}"[..24],
			Stages =
			[
				new CreateUpdateAdmissionWorkflowStageDto
				{
					Id = formStageId, Code = "FORM", Name = "Application form",
					StageType = AdmissionWorkflowStageType.Form, DisplayOrder = 0,
					IsEntryStage = true, IsRequired = true,
					ConfigurationJson = JsonSerializer.Serialize(new { formDefinitionId = formId, form = policy })
				},
				new CreateUpdateAdmissionWorkflowStageDto
				{
					Id = doneStageId, Code = "DONE", Name = "Ready for review",
					StageType = AdmissionWorkflowStageType.Approval, DisplayOrder = 1,
					IsTerminalStage = true, IsRequired = true
				}
			],
			Transitions =
			[
				new CreateUpdateAdmissionWorkflowTransitionDto
				{
					FromStageId = formStageId, ToStageId = doneStageId, Priority = 1, IsDefault = true
				}
			]
		};
		if (endsAtForm)
		{
			input.Stages.RemoveAt(1);
			input.Stages[0].IsTerminalStage = true;
			input.Transitions.Clear();
		}
		var workflow = await _workflowAppService.CreateAsync(input);
		return await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
	}
}
