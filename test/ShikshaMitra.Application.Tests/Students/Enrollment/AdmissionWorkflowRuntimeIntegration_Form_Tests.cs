using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Managed_Form_Should_Hold_The_Workflow_For_Verification_And_Audit_Corrections()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new Security.UserScope.UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Managed Form integration test"), true);
			var basicWorkflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(basicWorkflow.Id);
			var managedWorkflow = await CreateManagedFormWorkflowAsync(offering.ApplicationFormDefinitionId!.Value);
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(offering.Id);
				stored.ConfigureReferences(managedWorkflow.Id, stored.InquiryFormDefinitionId, stored.ApplicationFormDefinitionId);
				await _offeringRepository.UpdateAsync(stored, true);
			});

			var application = await _applicationAppService.StartAsync((await CreateApplicantAsync("Mira")).Id, offering.Id);
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
			workflow.Stages.Single(item => item.Code == "DONE").Status.ShouldBe(AdmissionRuntimeStageStatus.Active);
			var history = await _applicationAppService.GetFormHistoryAsync(application.Id);
			history.ShouldContain(item => item.ChangeType == "CorrectionRequested");
			history.Count(item => item.ChangeType == "SectionVerification").ShouldBe(2);
		}
	}

	private async Task<AdmissionWorkflowDto> CreateManagedFormWorkflowAsync(Guid formId)
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
		var workflow = await _workflowAppService.CreateAsync(new CreateUpdateAdmissionWorkflowDto
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
					StageType = AdmissionWorkflowStageType.Custom, DisplayOrder = 1,
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
		});
		return await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
	}
}
