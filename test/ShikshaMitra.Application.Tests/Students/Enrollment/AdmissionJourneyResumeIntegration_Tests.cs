using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Journey_resume_long_closure_cleanup_does_not_invalidate_its_own_snapshot()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, _, _, _) = await CreateCurrentJourneyApplicationAsync(AdmissionWorkflowStageType.Assessment);
		await _applicationAppService.EndJourneyAsync(application.Id, new() { Reason = "Slow resource cleanup", ConcurrencyStamp = application.ConcurrencyStamp! });
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<AdmissionApplicationDecisionHistory, Guid>>();
			var closure = await repository.GetAsync(x => x.ApplicationId == application.Id && x.Action == "journey-ended");
			// Fixture-only clock simulation: cleanup took two minutes after cancelling stages; no real delay or production audit edit.
			typeof(AdmissionApplicationDecisionHistory).GetProperty(nameof(AdmissionApplicationDecisionHistory.OccurredAt))!.SetValue(closure, closure.OccurredAt.AddMinutes(2));
			await repository.UpdateAsync(closure, true);
		});
		var preview = await _applicationAppService.GetResumePreviewAsync(application.Id);
		preview.Blockers.ShouldBeEmpty();
		(await _applicationAppService.ResumeJourneyAsync(application.Id, new() { Reason = "Continue same case", ConcurrencyStamp = preview.ConcurrencyStamp,
			ClosureHistoryId = preview.ClosureHistoryId!.Value, IdempotencyKey = "resume-long-cleanup" })).CurrentStageInstanceId.ShouldBe(application.CurrentStageInstanceId);
	}
	[Fact]
	public async Task Journey_resume_completed_seat_rechecks_capacity_and_rolls_back_before_safe_reacquisition()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		await _scopeRepository.InsertAsync(new ShikshaMitra.Security.UserScope.UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Resume seat fixture"), true);
		var initial = await CreateAndPublishWorkflowAsync();
		var offering = await CreateAvailableOfferingAsync(initial.Id);
		var managed = await CreateManagedSeatWorkflowAsync();
		await WithUnitOfWorkAsync(async () => { var saved = await _offeringRepository.GetAsync(offering.Id); saved.ConfigureReferences(managed.Id, saved.InquiryFormDefinitionId, saved.ApplicationFormDefinitionId); await _offeringRepository.UpdateAsync(saved, true); });
		var service = GetRequiredService<IAdmissionSeatAppService>();
		var first = await EnterSeatAsync(offering.Id, "Resume Seat", "resume-seat-entry");
		var seat = await service.GetAsync(first.Application.Id, first.Stage.Id);
		seat = await service.ReserveAsync(first.Application.Id, first.Stage.Id, new() { IdempotencyKey = "resume-seat-reserve", ConcurrencyStamp = seat.ConcurrencyStamp });
		seat = await service.ConfirmAsync(first.Application.Id, first.Stage.Id, new() { IdempotencyKey = "resume-seat-confirm", ConcurrencyStamp = seat.ConcurrencyStamp, ReservationConcurrencyStamp = seat.ReservationConcurrencyStamp });
		await _applicationAppService.AdvanceWorkflowAsync(first.Application.Id, new() { ExpectedCurrentStageInstanceId = first.Stage.Id, IdempotencyKey = "resume-seat-complete", ConcurrencyStamp = seat.ConcurrencyStamp });
		var application = await _applicationAppService.GetAsync(first.Application.Id);
		await _applicationAppService.EndJourneyAsync(application.Id, new() { Reason = "Seat temporarily released", ConcurrencyStamp = application.ConcurrencyStamp! });
		var second = await EnterSeatAsync(offering.Id, "Other Seat", "resume-other-seat-entry");
		var other = await service.GetAsync(second.Application.Id, second.Stage.Id);
		other = await service.ReserveAsync(second.Application.Id, second.Stage.Id, new() { IdempotencyKey = "resume-other-seat-reserve", ConcurrencyStamp = other.ConcurrencyStamp });
		var preview = await _applicationAppService.GetResumePreviewAsync(application.Id);
		var input = new ResumeAdmissionJourneyDto { Reason = "Recheck seat", ConcurrencyStamp = preview.ConcurrencyStamp, ClosureHistoryId = preview.ClosureHistoryId!.Value, IdempotencyKey = "resume-seat-case" };
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.ResumeJourneyAsync(application.Id, input))).Code.ShouldBe(AdmissionSeatStageErrorCodes.CapacityFull);
		(await _applicationAppService.GetAsync(application.Id)).Status.ShouldBe(AdmissionApplicationStatus.Withdrawn);
		(await _applicationAppService.GetWorkflowAsync(application.Id)).Status.ShouldBe(AdmissionWorkflowInstanceStatus.Cancelled);
		await service.ReleaseAsync(second.Application.Id, second.Stage.Id, new() { Reason = "Synthetic capacity test cleanup", IdempotencyKey = "resume-other-seat-release", ConcurrencyStamp = other.ConcurrencyStamp, ReservationConcurrencyStamp = other.ReservationConcurrencyStamp });
		await _applicationAppService.ResumeJourneyAsync(application.Id, input);
		var restored = await service.GetAsync(application.Id, first.Stage.Id);
		restored.Status.ShouldBe("confirmed"); restored.ReservationId.ShouldBe(seat.ReservationId);
		restored.Events.Count(x => x.Action == "seat-resume").ShouldBe(1);
	}

	[Fact]
	public async Task Journey_resume_closed_intake_shows_actual_blocker_without_restoration()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, _, _, offering) = await CreateCurrentJourneyApplicationAsync(AdmissionWorkflowStageType.Assessment);
		await _applicationAppService.EndJourneyAsync(application.Id, new() { Reason = "Paused", ConcurrencyStamp = application.ConcurrencyStamp! });
		await WithUnitOfWorkAsync(async () => { var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId); cycle.Close(TestUserId, DateTime.UtcNow); await _cycleRepository.UpdateAsync(cycle, true); });
		var preview = await _applicationAppService.GetResumePreviewAsync(application.Id);
		preview.CanResume.ShouldBeFalse(); preview.Blockers.ShouldContain(x => x.Contains("closed"));
		(await _applicationAppService.GetAsync(application.Id)).Status.ShouldBe(AdmissionApplicationStatus.Withdrawn);
	}
	[Theory]
	[InlineData(AdmissionWorkflowStageType.Form)]
	[InlineData(AdmissionWorkflowStageType.Assessment)]
	[InlineData(AdmissionWorkflowStageType.DocumentVerification)]
	[InlineData(AdmissionWorkflowStageType.Payment)]
	[InlineData(AdmissionWorkflowStageType.Approval)]
	public async Task Journey_resume_restores_same_application_and_exact_runtime(AdmissionWorkflowStageType preparation)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, _, preparationId, offering) = await CreateCurrentJourneyApplicationAsync(
			preparation is AdmissionWorkflowStageType.Form or AdmissionWorkflowStageType.Approval ? AdmissionWorkflowStageType.Assessment : preparation);
		if (preparation != AdmissionWorkflowStageType.Form)
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var entity = await GetRequiredService<IRepository<AdmissionApplication, Guid>>().GetAsync(application.Id);
				var runtime = await GetRequiredService<IRepository<AdmissionApplicationWorkflowInstance, Guid>>().GetAsync(x => x.ApplicationId == application.Id);
				var repository = GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>();
				var runtimeStages = await repository.GetListAsync(x => x.WorkflowInstanceId == runtime.Id);
				var current = runtimeStages.Single(x => x.Status == AdmissionRuntimeStageStatus.Active);
				current.Complete(TestUserId); await repository.UpdateAsync(current, true);
				var target = runtimeStages.Single(x => preparation == AdmissionWorkflowStageType.Approval ? x.StageType == preparation : x.Id == preparationId);
				target.Enter(); await repository.UpdateAsync(target, true);
				entity.Submit(); entity.SetCurrentStage(target.Id);
				await GetRequiredService<IRepository<AdmissionApplication, Guid>>().UpdateAsync(entity, true);
			});
			application = await _applicationAppService.GetAsync(application.Id);
		}
		var before = await _applicationAppService.GetWorkflowAsync(application.Id);
		var form = await _applicationAppService.GetFormAsync(application.Id);
		await _applicationAppService.EndJourneyAsync(application.Id, new() { Reason = "Parent paused", ConcurrencyStamp = application.ConcurrencyStamp! });
		var preview = await _applicationAppService.GetResumePreviewAsync(application.Id);
		preview.Blockers.ShouldBeEmpty(); preview.CanResume.ShouldBeTrue();
		preview.TargetStageId.ShouldBe(before.CurrentStageInstanceId);
		preview.TargetWorkspace.ShouldBe(preparation == AdmissionWorkflowStageType.Approval ? "Applications" : "Pipeline");
		var input = new ResumeAdmissionJourneyDto { Reason = "Parent returned", ConcurrencyStamp = preview.ConcurrencyStamp,
			ClosureHistoryId = preview.ClosureHistoryId!.Value, IdempotencyKey = Guid.NewGuid().ToString() };
		var resumed = await _applicationAppService.ResumeJourneyAsync(application.Id, input);
		resumed.Id.ShouldBe(application.Id); resumed.ApplicationNumber.ShouldBe(application.ApplicationNumber);
		resumed.Status.ShouldBe(application.Status); resumed.CurrentStageInstanceId.ShouldBe(before.CurrentStageInstanceId);
		if (preparation != AdmissionWorkflowStageType.Approval)
			(await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id })).Items.Single().Id.ShouldBe(application.Id);
		var after = await _applicationAppService.GetWorkflowAsync(application.Id);
		after.WorkflowDefinitionId.ShouldBe(before.WorkflowDefinitionId);
		after.Stages.Select(x => (x.Id, x.Status, x.EnteredAt, x.CompletedAt)).ShouldBe(before.Stages.Select(x => (x.Id, x.Status, x.EnteredAt, x.CompletedAt)));
		var afterForm = await _applicationAppService.GetFormAsync(application.Id);
		afterForm.FormInstanceId.ShouldBe(form.FormInstanceId); afterForm.FormVersion.ShouldBe(form.FormVersion);
		JsonSerializer.Serialize(afterForm.Values).ShouldBe(JsonSerializer.Serialize(form.Values));
		(await _applicationAppService.ResumeJourneyAsync(application.Id, input)).Id.ShouldBe(application.Id);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionApplicationDecisionHistory, Guid>>().CountAsync(x =>
			x.ApplicationId == application.Id && x.Action == "journey-resumed"))).ShouldBe(1);
		var current = await _applicationAppService.GetAsync(application.Id);
		await _applicationAppService.EndJourneyAsync(application.Id, new() { Reason = "Paused again", ConcurrencyStamp = current.ConcurrencyStamp! });
		await Should.ThrowAsync<Volo.Abp.BusinessException>(() => _applicationAppService.ResumeJourneyAsync(application.Id, input));
	}

	[Fact]
	public async Task Journey_resume_blocks_stale_stamp_wrong_closure_and_tenant_and_keeps_withdrawal()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, _, _, _) = await CreateCurrentJourneyApplicationAsync(AdmissionWorkflowStageType.Assessment);
		await _applicationAppService.EndJourneyAsync(application.Id, new() { Reason = "Stopped", ConcurrencyStamp = application.ConcurrencyStamp! });
		var preview = await _applicationAppService.GetResumePreviewAsync(application.Id);
		var input = new ResumeAdmissionJourneyDto { Reason = "Continue", ClosureHistoryId = preview.ClosureHistoryId!.Value,
			ConcurrencyStamp = "stale", IdempotencyKey = "resume-security" };
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.ResumeJourneyAsync(application.Id, input))).Code.ShouldBe(AdmissionJourneyResumeErrorCodes.Conflict);
		input.ConcurrencyStamp = preview.ConcurrencyStamp; input.ClosureHistoryId = Guid.NewGuid();
		await Should.ThrowAsync<BusinessException>(() => _applicationAppService.ResumeJourneyAsync(application.Id, input));
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(() => _applicationAppService.GetResumePreviewAsync(application.Id));
		(await _applicationAppService.GetAsync(application.Id)).Status.ShouldBe(AdmissionApplicationStatus.Withdrawn);
		(await _applicationAppService.GetDecisionHistoryAsync(application.Id)).Count.ShouldBe(1);
		(await _applicationAppService.StartAsync(application.ApplicantId, application.AdmissionOfferingId)).Id.ShouldBe(application.Id);
	}

	[Fact]
	public Task Journey_resume_keeps_part_paid_finance_account_dues_receipt_and_balance() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("ResumePartPaid");
		var fees = GetRequiredService<IAdmissionFeeCollectionAppService>();
		var prepared = await fees.PrepareAsync(fixture.Application.Id, new());
		var due = (await AccountDuesAsync(prepared.AccountId!.Value)).Single();
		var payment = await fees.CollectAsync(fixture.Application.Id, AdmissionCashPayment(due, 400m, "resume-payment-preserved"));
		var before = await fees.GetSummaryAsync(fixture.Application.Id);
		var application = await _applicationAppService.GetAsync(fixture.Application.Id);
		await _applicationAppService.EndJourneyAsync(application.Id, new() { Reason = "Temporarily stopped", ConcurrencyStamp = application.ConcurrencyStamp! });
		var preview = await _applicationAppService.GetResumePreviewAsync(application.Id);
		preview.Blockers.ShouldBeEmpty();
		await _applicationAppService.ResumeJourneyAsync(application.Id, new() { Reason = "Returned", ConcurrencyStamp = preview.ConcurrencyStamp,
			ClosureHistoryId = preview.ClosureHistoryId!.Value, IdempotencyKey = "resume-paid-case" });
		var after = await fees.GetSummaryAsync(application.Id);
		after.AccountId.ShouldBe(before.AccountId); after.ConfirmedPaidAmount.ShouldBe(400m);
		after.OutstandingAmount.ShouldBe(before.OutstandingAmount); after.OutstandingAmount.ShouldBe(600m);
		(await AccountDuesAsync(after.AccountId!.Value)).Single().Id.ShouldBe(due.Id);
		JsonSerializer.Serialize(after.Receipts).ShouldBe(JsonSerializer.Serialize(before.Receipts));
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Journey_resume_legacy_requires_trustworthy_current_stage(bool ambiguous)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, _, _, _) = await CreateCurrentJourneyApplicationAsync(AdmissionWorkflowStageType.Assessment);
		await WithUnitOfWorkAsync(async () =>
		{
			var entity = await _applicationRepository.GetAsync(application.Id);
			var workflowRepo = GetRequiredService<IRepository<AdmissionApplicationWorkflowInstance, Guid>>();
			var workflow = await workflowRepo.GetAsync(x => x.ApplicationId == entity.Id);
			var stageRepo = GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>();
			var target = entity.CurrentStageInstanceId;
			entity.EndJourney(TestUserId, "Legacy End Journey"); workflow.Cancel();
			foreach (var stage in await stageRepo.GetListAsync(x => x.WorkflowInstanceId == workflow.Id))
			{ stage.Cancel(TestUserId, "Legacy End Journey"); await stageRepo.UpdateAsync(stage, false); }
			await workflowRepo.UpdateAsync(workflow, false); await _applicationRepository.UpdateAsync(entity, false);
			await GetRequiredService<IRepository<AdmissionApplicationDecisionHistory, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId,
				entity.Id, AdmissionApplicationStatus.Withdrawn, "Legacy End Journey", TestUserId, "journey-ended", ambiguous ? Guid.NewGuid() : target), true);
		});
		var preview = await _applicationAppService.GetResumePreviewAsync(application.Id);
		preview.CanResume.ShouldBe(!ambiguous);
		if (ambiguous) preview.Blockers.Single().ShouldContain("could not be recovered");
		else (await _applicationAppService.ResumeJourneyAsync(application.Id, new() { Reason = "Legacy parent returned", ConcurrencyStamp = preview.ConcurrencyStamp,
			ClosureHistoryId = preview.ClosureHistoryId!.Value, IdempotencyKey = "legacy-resume" })).CurrentStageInstanceId.ShouldBe(application.CurrentStageInstanceId);
	}

	[Fact]
	public Task Journey_resume_completed_workflow_retains_finalization_boundary() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateDecisionWorkspaceOfferingAsync();
		var application = await StartFixtureApplicationAsync((await CreateApplicantAsync("Resume finalization")).Id, offering.Id);
		(await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
		application = await _applicationAppService.GetAsync(application.Id);
		await _applicationAppService.ApproveAsync(application.Id, new() { Reason = "Approved", ConcurrencyStamp = application.ConcurrencyStamp, IdempotencyKey = "resume-approve-boundary" });
		var workflow = await _applicationAppService.GetWorkflowAsync(application.Id);
		if (workflow.Status == AdmissionWorkflowInstanceStatus.Active)
		{
			application = await _applicationAppService.GetAsync(application.Id);
			await _applicationAppService.AdvanceWorkflowAsync(application.Id, new() { ExpectedCurrentStageInstanceId = workflow.CurrentStageInstanceId!.Value,
				ConcurrencyStamp = application.ConcurrencyStamp, IdempotencyKey = "resume-finish-decision" });
		}
		application = await _applicationAppService.GetAsync(application.Id);
		var before = await _applicationAppService.GetWorkflowAsync(application.Id);
		before.Status.ShouldBe(AdmissionWorkflowInstanceStatus.Completed);
		await _applicationAppService.EndJourneyAsync(application.Id, new() { Reason = "Paused at finalization", ConcurrencyStamp = application.ConcurrencyStamp! });
		var preview = await _applicationAppService.GetResumePreviewAsync(application.Id);
		preview.CanResume.ShouldBeTrue(); preview.TargetStageId.ShouldBeNull(); preview.TargetStageName.ShouldBe("Admission finalization");
		var resumed = await _applicationAppService.ResumeJourneyAsync(application.Id, new() { Reason = "Ready again", ConcurrencyStamp = preview.ConcurrencyStamp,
			ClosureHistoryId = preview.ClosureHistoryId!.Value, IdempotencyKey = "resume-finalization" });
		resumed.Status.ShouldBe(AdmissionApplicationStatus.Approved);
		var after = await _applicationAppService.GetWorkflowAsync(application.Id);
		after.Status.ShouldBe(before.Status); after.CompletedAt.ShouldBe(before.CompletedAt);
		resumed.HasReachedDecision.ShouldBe(true);
	});
	[Fact]
	public async Task Journey_resume_closure_keeps_original_state_in_immutable_history()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, _, _, _) = await CreateCurrentJourneyApplicationAsync(AdmissionWorkflowStageType.Assessment);
		var before = await _applicationAppService.GetWorkflowAsync(application.Id);
		await _applicationAppService.EndJourneyAsync(application.Id, new()
		{ Reason = "Resume regression fixture", ConcurrencyStamp = application.ConcurrencyStamp! });
		var closed = await WithUnitOfWorkAsync(() => GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionApplicationDecisionHistory, Guid>>()
			.GetAsync(x => x.ApplicationId == application.Id && x.Action == "journey-ended"));
		closed.MetadataJson.ShouldNotBeNullOrWhiteSpace();
		using var snapshot = JsonDocument.Parse(closed.MetadataJson!);
		snapshot.RootElement.GetProperty("Version").GetInt32().ShouldBe(1);
		snapshot.RootElement.GetProperty("PreviousStatus").GetInt32().ShouldBe((int)application.Status);
		snapshot.RootElement.GetProperty("CurrentStageId").GetGuid().ShouldBe(before.CurrentStageInstanceId!.Value);
		snapshot.RootElement.GetProperty("PendingStageIds").EnumerateArray().Select(x => x.GetGuid()).Order()
			.ShouldBe(before.Stages.Where(x => x.Status == AdmissionRuntimeStageStatus.Pending).Select(x => x.Id).Order());
	}
}
