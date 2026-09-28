using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Managed_documents_collected_during_form_reuse_evidence_but_cannot_be_reviewed_early()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, formId, documentId) = await CreateFormDocumentCollectionApplicationAsync(true);
		var service = GetRequiredService<IAdmissionDocumentVerificationAppService>();
		var workflow = await _applicationAppService.GetWorkflowAsync(application.Id);
		workflow.Stages.Single(item => item.Id == documentId).DocumentCollectionAvailable.ShouldBeTrue();
		var state = await service.GetAsync(application.Id, documentId);
		state.IsActive.ShouldBeFalse();
		state.CanCollectDuringForm.ShouldBeTrue();
		state = await UploadDocumentAsync(service, application.Id, documentId, state);
		state.CanComplete.ShouldBeFalse();
		var requirement = state.Requirements.Single();
		requirement.Status.ShouldBe("pendingReview");
		requirement.CanReview.ShouldBeFalse();
		requirement.CanWaive.ShouldBeFalse();
		var evidenceId = requirement.Evidence.Single().Id;
		await Should.ThrowAsync<BusinessException>(() => service.ReviewAsync(evidenceId,
			new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "accepted" }));
		await Should.ThrowAsync<BusinessException>(() => service.WaiveAsync(application.Id, documentId, "BIRTH",
			new() { ConcurrencyStamp = state.ConcurrencyStamp, Reason = "Cannot waive during collection" }));
		await Should.ThrowAsync<BusinessException>(() => service.RecordOriginalAsync(application.Id, documentId, "BIRTH",
			new() { ConcurrencyStamp = state.ConcurrencyStamp, Notes = "Cannot verify original during collection" }));

		(await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
		await AdvanceAssessmentAsync(application.Id, formId);
		state = await service.GetAsync(application.Id, documentId);
		state.IsActive.ShouldBeTrue();
		state.CanCollectDuringForm.ShouldBeFalse();
		state.Requirements.Single().Evidence.Single().Id.ShouldBe(evidenceId);
		state.Requirements.Single().CanReview.ShouldBeTrue();
		state = await service.ReviewAsync(evidenceId, new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "accepted" });
		state.CanComplete.ShouldBeTrue();
		state = await UploadDocumentAsync(service, application.Id, documentId, state);
		state.CanComplete.ShouldBeFalse();
		state.Requirements.Single().Evidence.Count.ShouldBe(2);
		state.Requirements.Single().Status.ShouldBe("pendingReview");
	}

	[Fact]
	public async Task Managed_documents_cannot_upload_early_without_a_configured_form_link()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, _, documentId) = await CreateFormDocumentCollectionApplicationAsync(false);
		var service = GetRequiredService<IAdmissionDocumentVerificationAppService>();
		var state = await service.GetAsync(application.Id, documentId);
		state.CanCollectDuringForm.ShouldBeFalse();
		state.Requirements.Single().CanUpload.ShouldBeFalse();
		await Should.ThrowAsync<BusinessException>(() => UploadDocumentAsync(service, application.Id, documentId, state));
	}

	private async Task<(AdmissionApplicationDto Application, Guid FormId, Guid DocumentId)> CreateFormDocumentCollectionApplicationAsync(bool collectEarly)
	{
		await _scopeRepository.InsertAsync(new Security.UserScope.UserScopeAssignment(
			Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Form document collection test"), true);
		var basicWorkflow = await CreateAndPublishWorkflowAsync();
		var offering = await CreateAvailableOfferingAsync(basicWorkflow.Id);
		var source = await CreateManagedFormWorkflowAsync(offering.ApplicationFormDefinitionId!.Value);
		var draft = await _workflowAppService.CreateNewVersionAsync(source.Id);
		var input = ToUpdateInput(draft);
		var form = input.Stages.Single(item => item.IsEntryStage);
		form.ConfigurationJson = JsonSerializer.Serialize(new
		{
			formDefinitionId = offering.ApplicationFormDefinitionId,
			form = new AdmissionFormStagePolicy { Version = 1, Enabled = true, Responsibility = "shared", AutoAdvanceOnSubmit = false }
		});
		var done = input.Stages.Single(item => item.IsTerminalStage);
		done.DisplayOrder = 2;
		var policy = HandoffPolicy(new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate" })
			with { CollectionFormStageCode = collectEarly ? form.Code : null };
		var documentDefinitionId = Guid.NewGuid();
		input.Stages.Add(new()
		{
			Id = documentDefinitionId, Code = "DOCUMENTS", Name = "Verify documents", DisplayOrder = 1,
			StageType = AdmissionWorkflowStageType.DocumentVerification, IsRequired = true,
			ConfigurationJson = JsonSerializer.Serialize(new { documentVerification = policy })
		});
		input.Transitions =
		[
			new() { FromStageId = form.Id!.Value, ToStageId = documentDefinitionId, IsDefault = true },
			new() { FromStageId = documentDefinitionId, ToStageId = done.Id!.Value, IsDefault = true }
		];
		draft = await _workflowAppService.UpdateAsync(draft.Id, input);
		var published = await _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp);
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await _offeringRepository.GetAsync(offering.Id);
			stored.ConfigureReferences(published.Id, stored.InquiryFormDefinitionId, stored.ApplicationFormDefinitionId);
			await _offeringRepository.UpdateAsync(stored, true);
		});
		var application = await StartFixtureApplicationAsync((await CreateApplicantAsync("Anaya")).Id, offering.Id);
		var workflow = await _applicationAppService.GetWorkflowAsync(application.Id);
		return (application, workflow.Stages.Single(item => item.Code == form.Code).Id,
			workflow.Stages.Single(item => item.Code == "DOCUMENTS").Id);
	}
}
