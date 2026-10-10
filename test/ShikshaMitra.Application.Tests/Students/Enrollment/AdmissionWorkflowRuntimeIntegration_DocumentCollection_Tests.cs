using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Managed_documents_prepared_during_form_reuse_evidence_without_advancing_the_journey()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, formId, documentId) = await CreateFormDocumentCollectionApplicationAsync(true);
		var service = GetRequiredService<IAdmissionDocumentVerificationAppService>();
		var workflow = await _applicationAppService.GetWorkflowAsync(application.Id);
		workflow.Stages.Single(item => item.Id == documentId).DocumentCollectionAvailable.ShouldBeFalse();
		var state = await service.GetAsync(application.Id, documentId);
		state.IsActive.ShouldBeFalse();
		state.CanCollectDuringForm.ShouldBeFalse();
		state.Requirements.Single().CanUpload.ShouldBeTrue();
		state = await UploadDocumentAsync(service, application.Id, documentId, state);
		state.CanComplete.ShouldBeFalse();
		var requirement = state.Requirements.Single();
		requirement.Status.ShouldBe("pendingReview");
		requirement.CanReview.ShouldBeTrue();
		requirement.CanWaive.ShouldBeFalse();
		var evidenceId = requirement.Evidence.Single().Id;
		state = await service.ReviewAsync(evidenceId, new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "accepted" });
		state.CanComplete.ShouldBeFalse();
		(await _applicationAppService.GetWorkflowAsync(application.Id)).CurrentStageInstanceId.ShouldBe(formId);
		await Should.ThrowAsync<BusinessException>(() => service.RecordOriginalAsync(application.Id, documentId, "BIRTH",
			new() { ConcurrencyStamp = state.ConcurrencyStamp, Notes = "Cannot verify original during collection" }));

		(await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
		var formService = GetRequiredService<IAdmissionFormStageAppService>();
		var submitted = await formService.GetAsync(application.Id, formId);
		await formService.VerifySectionAsync(application.Id, formId, submitted.Sections.Single(item => item.Required).SectionId,
			new() { ConcurrencyStamp = submitted.ConcurrencyStamp, Verified = true });
		await AdvanceAssessmentAsync(application.Id, formId);
		state = await service.GetAsync(application.Id, documentId);
		state.IsActive.ShouldBeTrue();
		state.CanCollectDuringForm.ShouldBeFalse();
		state.Requirements.Single().Evidence.Single().Id.ShouldBe(evidenceId);
		state.Requirements.Single().CanReview.ShouldBeFalse();
		state.CanComplete.ShouldBeTrue();
		state = await UploadDocumentAsync(service, application.Id, documentId, state);
		state.CanComplete.ShouldBeFalse();
		state.Requirements.Single().Evidence.Count.ShouldBe(2);
		state.Requirements.Single().Status.ShouldBe("pendingReview");
	}

	[Fact]
	public async Task Managed_documents_can_be_prepared_in_a_future_stage_without_a_form_collection_link()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, _, documentId) = await CreateFormDocumentCollectionApplicationAsync(false);
		var service = GetRequiredService<IAdmissionDocumentVerificationAppService>();
		var state = await service.GetAsync(application.Id, documentId);
		state.CanCollectDuringForm.ShouldBeFalse();
		state.Requirements.Single().CanUpload.ShouldBeTrue();
		var current = (await _applicationAppService.GetWorkflowAsync(application.Id)).CurrentStageInstanceId;
		state = await UploadDocumentAsync(service, application.Id, documentId, state);
		state.Requirements.Single().Status.ShouldBe("pendingReview");
		state.CanComplete.ShouldBeFalse();
		(await _applicationAppService.GetWorkflowAsync(application.Id)).CurrentStageInstanceId.ShouldBe(current);
	}

	private async Task<(AdmissionApplicationDto Application, Guid FormId, Guid DocumentId)> CreateFormDocumentCollectionApplicationAsync(bool collectEarly)
	{
		var (application, formId, documentId, _) = await CreateCurrentJourneyApplicationAsync(
			AdmissionWorkflowStageType.DocumentVerification, collectDuringForm: collectEarly);
		return (application, formId, documentId);
	}
}
