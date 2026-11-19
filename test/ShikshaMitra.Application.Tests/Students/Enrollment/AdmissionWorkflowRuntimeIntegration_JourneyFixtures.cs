using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private async Task<(AdmissionApplicationDto Application, Guid FormId, Guid PreparationId, AdmissionOffering Offering)>
		CreateCurrentJourneyApplicationAsync(AdmissionWorkflowStageType preparationType, bool scheduling = false,
			bool formIsFuture = false, bool collectDuringForm = false)
	{
		await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
			userId: TestUserId, description: "Current admission journey test"), true);
		var formDefinition = Guid.NewGuid();
		var decisionDefinition = Guid.NewGuid();
		var initial = await _workflowAppService.CreateAsync(new()
		{
			Name = "Current journey fixture", Code = $"CURRENT_{Guid.NewGuid():N}"[..24],
			Stages =
			[
				new() { Id = formDefinition, Code = "FORM", Name = "Application form", StageType = AdmissionWorkflowStageType.Form,
					DisplayOrder = 0, IsEntryStage = true, IsRequired = true },
				new() { Id = decisionDefinition, Code = "DECISION", Name = "Admission decision", StageType = AdmissionWorkflowStageType.Approval,
					DisplayOrder = 1, IsTerminalStage = true, IsRequired = true }
			],
			Transitions = [new() { FromStageId = formDefinition, ToStageId = decisionDefinition, IsDefault = true }]
		});
		// The form is created by the existing offering fixture before the configured process can be published.
		var offering = await CreateAvailableOfferingAsync(initial.Id, assertValid: false);
		var formPolicy = new AdmissionFormStagePolicy
		{
			Version = 1, Enabled = true, Responsibility = "shared", AllowSaveAndResume = true,
			AllowLateSubmission = true, AutoAdvanceOnSubmit = false, AllowCorrectionRequests = true,
			MaxSubmissionAttempts = 3, RequireSectionVerification = true, VerificationSectionCodes = ["STUDENT"]
		};
		var preparationId = Guid.NewGuid();
		var effectiveType = formIsFuture ? AdmissionWorkflowStageType.Assessment : preparationType;
		var preparationJson = effectiveType switch
		{
			AdmissionWorkflowStageType.Assessment => JsonSerializer.Serialize(new { assessment = new
			{
				version = 1, enabled = true, schedulingEnabled = scheduling, attendanceRequired = true,
				scoringEnabled = true, totalMarks = 100, passingMarks = 40, reviewRequired = false, maxAttempts = 2
			} }),
			AdmissionWorkflowStageType.DocumentVerification => JsonSerializer.Serialize(new { documentVerification =
				HandoffPolicy(new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate" })
					with { CollectionFormStageCode = collectDuringForm ? "FORM" : null } }),
			_ => null
		};
		var stages = new List<CreateUpdateAdmissionWorkflowStageDto>
		{
			new() { Id = formDefinition, Code = "FORM", Name = "Application form", StageType = AdmissionWorkflowStageType.Form,
				DisplayOrder = formIsFuture ? 1 : 0, IsEntryStage = !formIsFuture, IsRequired = true,
				ConfigurationJson = JsonSerializer.Serialize(new { formDefinitionId = offering.ApplicationFormDefinitionId, form = formPolicy }) },
			new() { Id = preparationId, Code = "PREPARATION", Name = "Prepare next stage", StageType = effectiveType,
				DisplayOrder = formIsFuture ? 0 : 1, IsEntryStage = formIsFuture, IsRequired = true, ConfigurationJson = preparationJson },
			new() { Id = decisionDefinition, Code = "DECISION", Name = "Admission decision", StageType = AdmissionWorkflowStageType.Approval,
				DisplayOrder = 2, IsTerminalStage = true, IsRequired = true }
		};
		stages = stages.OrderBy(item => item.DisplayOrder).ToList();
		var workflow = await _workflowAppService.CreateAsync(new()
		{
			Name = "Current configured journey", Code = $"JOURNEY_{Guid.NewGuid():N}"[..24], Stages = stages,
			Transitions = stages.Zip(stages.Skip(1), (from, to) => new CreateUpdateAdmissionWorkflowTransitionDto
			{
				FromStageId = from.Id!.Value, ToStageId = to.Id!.Value, IsDefault = true
			}).ToList()
		});
		workflow = await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await _offeringRepository.GetAsync(offering.Id);
			stored.ConfigureReferences(workflow.Id, stored.InquiryFormDefinitionId, stored.ApplicationFormDefinitionId);
			await _offeringRepository.UpdateAsync(stored, true);
		});
		var application = await StartFixtureApplicationAsync((await CreateApplicantAsync("Current journey QA")).Id, offering.Id);
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		return (application, runtime.Stages.Single(item => item.Code == "FORM").Id,
			runtime.Stages.Single(item => item.Code == "PREPARATION").Id, offering);
	}
}
