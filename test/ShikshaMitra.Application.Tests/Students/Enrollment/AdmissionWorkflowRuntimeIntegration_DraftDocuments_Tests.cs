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
	[Fact]
	public async Task Draft_documents_advance_to_blank_form_after_acceptance_and_replay_is_idempotent()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate" }), documentsBeforeForm: true);
		var service = GetRequiredService<IAdmissionDocumentVerificationAppService>();
		var state = await service.GetAsync(fixture.Application.Id, fixture.StageId);
		state = await UploadDocumentAsync(service, fixture.Application.Id, fixture.StageId, state);
		state = await service.ReviewAsync(state.Requirements.Single().Evidence.Last().Id, new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "accepted" });
		state.CanComplete.ShouldBeTrue();
		state.Blockers.ShouldBeEmpty();
		var input = new AdvanceAdmissionWorkflowDto { ExpectedCurrentStageInstanceId = fixture.StageId, ConcurrencyStamp = state.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid().ToString() };
		var advanced = await _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id, input);
		var replay = await _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id, input);
		replay.CurrentStageInstanceId.ShouldBe(advanced.CurrentStageInstanceId);
		(await WithUnitOfWorkAsync(() => _historyRepository.CountAsync(history => history.IdempotencyKey == input.IdempotencyKey))).ShouldBe(1);
		advanced.CurrentStageInstanceId.ShouldNotBe(fixture.StageId);
		advanced.Stages.Single(stage => stage.Id == advanced.CurrentStageInstanceId).StageType.ShouldBe(AdmissionWorkflowStageType.Form);
		var application = await _applicationAppService.GetAsync(fixture.Application.Id);
		application.Status.ShouldBe(AdmissionApplicationStatus.Draft);
		application.SubmittedAt.ShouldBeNull();
		(await service.GetAsync(fixture.Application.Id, fixture.StageId)).CanComplete.ShouldBeFalse();
	}

	[Fact]
	public async Task Draft_documents_still_require_an_eligible_next_stage_route()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate" }), documentsBeforeForm: true);
		var service = GetRequiredService<IAdmissionDocumentVerificationAppService>();
		var state = await service.GetAsync(fixture.Application.Id, fixture.StageId);
		state = await UploadDocumentAsync(service, fixture.Application.Id, fixture.StageId, state);
		state = await service.ReviewAsync(state.Requirements.Single().Evidence.Last().Id, new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "accepted" });
		await WithUnitOfWorkAsync(async () =>
		{
			var stage = await GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>().GetAsync(fixture.StageId);
			await GetRequiredService<IRepository<AdmissionWorkflowTransitionDefinition, Guid>>().DeleteAsync(route => route.FromStageId == stage.DefinitionStageId, true);
		});
		state = await service.GetAsync(fixture.Application.Id, fixture.StageId);
		state.CanComplete.ShouldBeFalse();
		state.Blockers.ShouldContain("Configure an eligible next-stage route before completing document verification.");
		await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id,
			new() { ExpectedCurrentStageInstanceId = fixture.StageId, ConcurrencyStamp = state.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid().ToString() }));
		(await _applicationAppService.GetWorkflowAsync(fixture.Application.Id)).CurrentStageInstanceId.ShouldBe(fixture.StageId);
	}

	[Theory]
	[InlineData("missing")]
	[InlineData("pendingReview")]
	[InlineData("rejected")]
	[InlineData("replacement")]
	public async Task Draft_documents_reject_unaccepted_latest_required_evidence_in_readiness_and_command(string status)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate" }), documentsBeforeForm: true);
		var service = GetRequiredService<IAdmissionDocumentVerificationAppService>();
		var state = await service.GetAsync(fixture.Application.Id, fixture.StageId);
		if (status != "missing") state = await UploadDocumentAsync(service, fixture.Application.Id, fixture.StageId, state);
		if (status is "rejected" or "replacement")
			state = await service.ReviewAsync(state.Requirements.Single().Evidence.Last().Id, new() { ConcurrencyStamp = state.ConcurrencyStamp,
				Decision = status == "rejected" ? "rejected" : "accepted", Reason = "Unreadable copy" });
		if (status == "replacement") state = await UploadDocumentAsync(service, fixture.Application.Id, fixture.StageId, state);
		state.CanComplete.ShouldBeFalse();
		var failure = await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id,
			new() { ExpectedCurrentStageInstanceId = fixture.StageId, ConcurrencyStamp = state.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid().ToString() }));
		failure.Code.ShouldBe(AdmissionDocumentErrorCodes.EvidenceRequired);
		(await _applicationAppService.GetWorkflowAsync(fixture.Application.Id)).CurrentStageInstanceId.ShouldBe(fixture.StageId);
	}
}
