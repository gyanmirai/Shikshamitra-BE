using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(AdmissionTrack.Rte, AdmissionEntryMode.DirectRegistration)]
	[InlineData(AdmissionTrack.Management, AdmissionEntryMode.DirectRegistration)]
	[InlineData(AdmissionTrack.Staff, AdmissionEntryMode.DirectRegistration)]
	[InlineData(AdmissionTrack.Management, AdmissionEntryMode.Both)]
	public Task Special_registration_pipeline_preserves_scope_and_permanent_decision_handover(AdmissionTrack track, AdmissionEntryMode mode) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateSpecialRegistrationOfferingAsync(track, mode);
			var input = SpecialRegistrationInput(offering.Id);
			input.GuardianEmail = "ravi.pipeline@example.test";
			var application = await _applicationAppService.StartDirectRegistrationAsync(input);
			var filter = new AdmissionPipelineListInput { MyCases = false, AdmissionOfferingId = offering.Id };
			var row = (await _applicationAppService.GetPipelineAsync(filter)).Items.Single();
			row.Id.ShouldBe(application.Id);
			row.InquiryId.ShouldBeNull();
			row.InquiryNumber.ShouldBeNull();
			row.SourceInquiryAvailable.ShouldBeFalse();
			row.StageType.ShouldBe(AdmissionWorkflowStageType.Form);
			row.ContactPhone.ShouldBe(input.GuardianMobile);
			row.ContactEmail.ShouldBe(input.GuardianEmail);
			row.OpenFollowUpCount.ShouldBe(0);
			row.NextFollowUpAt.ShouldBeNull();
			row.HasOverdueFollowUp.ShouldBeFalse();
			foreach (var search in new[] { input.GuardianMobile, input.GuardianEmail, application.ApplicationNumber })
				(await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id, Search = search })).Items.Single().Id.ShouldBe(application.Id);
			(await _applicationAppService.GetPipelineOptionsAsync(filter)).Stages.ShouldContain(item => item.Code == "FORM");
			(await _applicationAppService.GetPipelineAsync(new() { MyCases = true, AdmissionOfferingId = offering.Id })).TotalCount.ShouldBe(0);
			(await _applicationAppService.GetPipelineAsync(new() { MyCases = false, BoardId = Guid.NewGuid() })).TotalCount.ShouldBe(0);
			(await _applicationAppService.GetListAsync(new() { DecisionWorkspaceOnly = true, AdmissionOfferingId = offering.Id })).TotalCount.ShouldBe(0);
			await Should.ThrowAsync<EntityNotFoundException>(() => _applicationAppService.GetFollowUpsAsync(application.Id));
			using (_currentTenant.Change(Guid.NewGuid()))
				(await _applicationAppService.GetPipelineAsync(filter)).TotalCount.ShouldBe(0);
			using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity([
				new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()), new Claim(AbpClaimTypes.UserName, "special-pipeline-no-scope")], "Test"))))
			{
				(await _applicationAppService.GetPipelineAsync(filter)).TotalCount.ShouldBe(0);
				(await _applicationAppService.GetPipelineOptionsAsync(filter)).Stages.ShouldBeEmpty();
			}

			(await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
			var decision = await _applicationAppService.GetAsync(application.Id);
			decision.HasReachedDecision.ShouldBe(true);
			(await _applicationAppService.GetPipelineAsync(filter)).TotalCount.ShouldBe(0);
			(await _applicationAppService.GetPipelineOptionsAsync(filter)).Stages.ShouldBeEmpty();
			(await _applicationAppService.GetListAsync(new() { DecisionWorkspaceOnly = true, AdmissionOfferingId = offering.Id })).Items.Single().Id.ShouldBe(application.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var entity = await _applicationRepository.GetAsync(application.Id);
				entity.ReopenForManagedReview();
				entity.SetCurrentStage(row.CurrentStageInstanceId!.Value);
				await _applicationRepository.UpdateAsync(entity, true);
			});
			(await _applicationAppService.GetPipelineAsync(filter)).TotalCount.ShouldBe(0);
			var corrected = (await _applicationAppService.GetListAsync(new() { DecisionWorkspaceOnly = true, AdmissionOfferingId = offering.Id })).Items.Single();
			corrected.Id.ShouldBe(application.Id);
			corrected.DecisionEnteredAt.ShouldBe(decision.DecisionEnteredAt);
		});
}
