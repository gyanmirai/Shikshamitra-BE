using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Admissions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Journey_closed_before_decision_remains_in_applications_without_faking_stage_entry()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, _, _, offering) = await CreateCurrentJourneyApplicationAsync(AdmissionWorkflowStageType.Assessment);
		(await _applicationAppService.GetListAsync(new() { AdmissionOfferingId = offering.Id, DecisionWorkspaceOnly = true })).TotalCount.ShouldBe(0);
		(await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id })).Items.Single().Id.ShouldBe(application.Id);
		await _applicationAppService.EndJourneyAsync(application.Id, new() { Reason = "Family withdrew before document review", ConcurrencyStamp = application.ConcurrencyStamp.ShouldNotBeNull() });
		var closed = await _applicationAppService.GetAsync(application.Id);
		closed.Status.ShouldBe(AdmissionApplicationStatus.Withdrawn);
		closed.HasReachedDecision.ShouldBe(false);
		closed.DecisionEnteredAt.ShouldBeNull();
		var archive = await _applicationAppService.GetListAsync(new()
		{
			AdmissionOfferingId = offering.Id, DecisionWorkspaceOnly = true, Status = AdmissionApplicationStatus.Withdrawn,
			Search = application.ApplicationNumber, MaxResultCount = 1
		});
		archive.TotalCount.ShouldBe(1);
		archive.Items.Single().Id.ShouldBe(application.Id);
		archive.Items.Single().HasReachedDecision.ShouldBe(false);
		archive.Items.Single().DecisionEnteredAt.ShouldBeNull();
		(await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id })).TotalCount.ShouldBe(0);
	}

	[Fact]
	public async Task Journey_future_form_can_save_a_draft_but_cannot_submit_before_its_current_stage()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, _, _, _) = await CreateCurrentJourneyApplicationAsync(AdmissionWorkflowStageType.Form, formIsFuture: true);
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		var stage = runtime.Stages.Single(item => item.StageType == AdmissionWorkflowStageType.Form);
		var service = GetRequiredService<IAdmissionFormStageAppService>();
		var summary = await service.GetAsync(application.Id, stage.Id);
		summary.CanSaveDraft.ShouldBeTrue();
		summary.CanSubmit.ShouldBeFalse();
		await _applicationAppService.UpdateFormValuesAsync(application.Id, new() { ConcurrencyStamp = summary.ConcurrencyStamp, Values = [] });
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.SubmitAsync(application.Id)))
			.Code.ShouldBe(AdmissionFormStageErrorCodes.InvalidState);
		(await _applicationAppService.GetWorkflowAsync(application.Id)).CurrentStageInstanceId.ShouldBe(runtime.CurrentStageInstanceId);
		(await _applicationAppService.GetAsync(application.Id)).Status.ShouldBe(AdmissionApplicationStatus.Draft);
	}

	[Fact]
	public async Task Journey_future_assessment_can_record_evidence_without_entering_or_completing_the_stage()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, _, stageId, _) = await CreateCurrentJourneyApplicationAsync(AdmissionWorkflowStageType.Assessment);
		var current = application.CurrentStageInstanceId;
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var state = await service.GetAsync(application.Id, stageId);
		state.CanBook.ShouldBeTrue();
		state.CanComplete.ShouldBeFalse();
		state.Blockers.ShouldNotContain("This assessment stage is not active.");
		state = await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = state.ConcurrencyStamp });
		state.CanRecordAttendance.ShouldBeTrue();
		state = await service.RecordAttendanceAsync(state.Attempts.Single().Id,
			new() { Attendance = "present", ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp });
		state.CanRecordResult.ShouldBeTrue();
		state = await service.RecordResultAsync(state.Attempts.Single().Id,
			new() { Marks = 90, ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp });
		state.Outcome.ShouldBe("passed");
		state.CanComplete.ShouldBeFalse();
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		runtime.CurrentStageInstanceId.ShouldBe(current);
		runtime.Stages.Single(item => item.Id == stageId).Status.ShouldBe(AdmissionRuntimeStageStatus.Pending);
		(await Should.ThrowAsync<BusinessException>(() => AdvanceAssessmentAsync(application.Id, stageId)))
			.Code.ShouldBe(AdmissionApplicationErrorCodes.InvalidCurrentStage);
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.ApproveAsync(application.Id,
			new() { ConcurrencyStamp = state.ConcurrencyStamp }))).Code.ShouldBe(AdmissionErrorCodes.ApplicationDecisionInvalidState);
		await _applicationAppService.EndJourneyAsync(application.Id, new() { Reason = "Withdraw after assessment", ConcurrencyStamp = state.ConcurrencyStamp });
		var retained = await service.GetAsync(application.Id, stageId);
		retained.Attempts.Single().Marks.ShouldBe(90);
		retained.Attempts.Single().Status.ShouldNotBe("cancelled");
		retained.CanRecordResult.ShouldBeFalse();
	}

	[Fact]
	public async Task Journey_end_preserves_audit_cancels_unused_booking_and_retries_without_duplicate_history()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, _, stageId, offering) = await CreateCurrentJourneyApplicationAsync(AdmissionWorkflowStageType.Assessment, scheduling: true);
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		var (seatPoolId, reservationId, scheduledId, completedId) = await WithUnitOfWorkAsync(async () =>
		{
			var pool = new AdmissionSeatPool(Guid.NewGuid(), TestTenantId, offering.Id, 2);
			pool.Reserve(false);
			await GetRequiredService<IRepository<AdmissionSeatPool, Guid>>().InsertAsync(pool, true);
			var reservation = new AdmissionSeatReservation(Guid.NewGuid(), TestTenantId, pool.Id, application.Id,
				stageId, AdmissionSeatReservationStatus.Reserved, DateTime.UtcNow, DateTime.UtcNow.AddDays(2), TestUserId, "Held seat");
			await GetRequiredService<IRepository<AdmissionSeatReservation, Guid>>().InsertAsync(reservation, true);
			var management = new AdmissionInquiryManagement(Guid.NewGuid(), TestTenantId, application.InquiryId!.Value,
				null, "{}", DateTime.UtcNow, null, null);
			await GetRequiredService<IRepository<AdmissionInquiryManagement, Guid>>().InsertAsync(management, true);
			var scheduled = new AdmissionInquiryFollowUp(Guid.NewGuid(), TestTenantId, management.InquiryId, management.Id,
				TestUserId, DateTime.UtcNow.AddDays(1), "Admission follow-up");
			var completed = new AdmissionInquiryFollowUp(Guid.NewGuid(), TestTenantId, management.InquiryId, management.Id,
				TestUserId, DateTime.UtcNow, "Earlier follow-up");
			completed.Complete(TestUserId, DateTime.UtcNow, "Parent contacted");
			var followUps = GetRequiredService<IRepository<AdmissionInquiryFollowUp, Guid>>();
			await followUps.InsertAsync(scheduled, true);
			await followUps.InsertAsync(completed, true);
			return (pool.Id, reservation.Id, scheduled.Id, completed.Id);
		});
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var session = await service.CreateSessionAsync(new()
		{
			AdmissionOfferingId = offering.Id, WorkflowStageDefinitionId = runtime.Stages.Single(item => item.Id == stageId).DefinitionStageId,
			Name = "Journey cancellation QA", StartsAt = DateTimeOffset.UtcNow.AddDays(1), EndsAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
			Location = "Room A", Capacity = 2
		});
		var state = await service.BookAsync(application.Id, stageId, new() { SessionId = session.Id, ConcurrencyStamp = application.ConcurrencyStamp });
		var originalForm = await _applicationAppService.GetFormAsync(application.Id);
		var command = new EndAdmissionJourneyDto { Reason = "  Family selected another school.  ", ConcurrencyStamp = state.ConcurrencyStamp };
		var closed = await _applicationAppService.EndJourneyAsync(application.Id, command);
		closed.Status.ShouldBe(AdmissionApplicationStatus.Withdrawn);
		closed.CanEndJourney.ShouldBeFalse();
		closed.CurrentStageInstanceId.ShouldBeNull();
		closed.DecisionReason.ShouldBe("Family selected another school.");
		closed.DecisionBy.ShouldBe(TestUserId);
		closed.DecisionAt.ShouldNotBeNull();
		var after = await _applicationAppService.GetWorkflowAsync(application.Id);
		after.Status.ShouldBe(AdmissionWorkflowInstanceStatus.Cancelled);
		after.Stages.ShouldAllBe(item => item.Status == AdmissionRuntimeStageStatus.Cancelled);
		(await service.GetAsync(application.Id, stageId)).Attempts.Single().Status.ShouldBe("cancelled");
		(await service.GetSessionsAsync(offering.Id, session.WorkflowStageDefinitionId)).Single(item => item.Id == session.Id).BookedCount.ShouldBe(0);
		(await _applicationAppService.GetFormAsync(application.Id)).Values.Count.ShouldBe(originalForm.Values.Count);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<AdmissionSeatPool, Guid>>().GetAsync(seatPoolId)).OccupiedCount.ShouldBe(0);
			var released = await GetRequiredService<IRepository<AdmissionSeatReservation, Guid>>().GetAsync(reservationId);
			released.Status.ShouldBe(AdmissionSeatReservationStatus.Released);
			released.LastReason.ShouldBe(closed.DecisionReason);
			var followUps = GetRequiredService<IRepository<AdmissionInquiryFollowUp, Guid>>();
			var cancelled = await followUps.GetAsync(scheduledId);
			cancelled.Status.ShouldBe(AdmissionInquiryFollowUpStatus.Cancelled);
			cancelled.CompletionNote.ShouldBe(closed.DecisionReason);
			(await followUps.GetAsync(completedId)).Status.ShouldBe(AdmissionInquiryFollowUpStatus.Completed);
		});
		(await _applicationAppService.GetDecisionHistoryAsync(application.Id)).Count.ShouldBe(1);
		(await _applicationAppService.EndJourneyAsync(application.Id, command)).DecisionAt.ShouldBe(closed.DecisionAt);
		(await _applicationAppService.GetDecisionHistoryAsync(application.Id)).Count.ShouldBe(1);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionActivity, Guid>>().CountAsync(item =>
			item.ApplicationId == application.Id && item.ActivityType == "AdmissionJourneyEnded"))).ShouldBe(1);
		(await service.GetAsync(application.Id, stageId)).CanBook.ShouldBeFalse();
		await Should.ThrowAsync<BusinessException>(() => service.BookAsync(application.Id, stageId, new() { SessionId = session.Id, ConcurrencyStamp = closed.ConcurrencyStamp }));
		await Should.ThrowAsync<BusinessException>(() => _applicationAppService.EndJourneyAsync(application.Id,
			new() { Reason = "Changed withdrawal reason", ConcurrencyStamp = closed.ConcurrencyStamp! }));
	}

	[Fact]
	public async Task Journey_end_rejects_missing_reason_stale_stamp_and_cross_scope_before_writing()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, _, _, _) = await CreateCurrentJourneyApplicationAsync(AdmissionWorkflowStageType.Assessment);
		await Should.ThrowAsync<AbpValidationException>(() => _applicationAppService.EndJourneyAsync(application.Id,
			new() { Reason = " ", ConcurrencyStamp = application.ConcurrencyStamp! }));
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.EndJourneyAsync(application.Id,
			new() { Reason = "Parent withdrawal", ConcurrencyStamp = "stale" }))).Code.ShouldBe(AdmissionApplicationErrorCodes.ConcurrencyConflict);
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<EntityNotFoundException>(() => _applicationAppService.EndJourneyAsync(application.Id,
				new() { Reason = "Other tenant", ConcurrencyStamp = application.ConcurrencyStamp! }));
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()), new Claim(AbpClaimTypes.UserName, "unscoped-staff")], "Test"))))
			await Should.ThrowAsync<EntityNotFoundException>(() => _applicationAppService.EndJourneyAsync(application.Id,
				new() { Reason = "Outside scope", ConcurrencyStamp = application.ConcurrencyStamp! }));
		(await _applicationAppService.GetAsync(application.Id)).Status.ShouldBe(AdmissionApplicationStatus.Draft);
		(await _applicationAppService.GetDecisionHistoryAsync(application.Id)).ShouldBeEmpty();
	}

	[Fact]
	public async Task Journey_future_fee_selection_saves_without_advancing_or_creating_payment_evidence()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, _, _, offering) = await CreateCurrentJourneyApplicationAsync(AdmissionWorkflowStageType.Payment);
		var before = await _applicationAppService.GetWorkflowAsync(application.Id);
		var stage = before.Stages.Single(item => item.StageType == AdmissionWorkflowStageType.Payment);
		stage.Status.ShouldBe(AdmissionRuntimeStageStatus.Pending);
		var service = GetRequiredService<IAdmissionFeeSelectionAppService>();
		var state = await service.GetAsync(application.Id);
		state.CanEdit.ShouldBeTrue();
		state.OwnerStageId.ShouldBe(stage.Id);
		var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
		var structure = await CreateFeeSelectionReferenceAsync(offering, cycle.AcademicYearId);
		state = await service.SaveAsync(application.Id, new() { StructureId = structure.Id, ConcurrencyStamp = state.ConcurrencyStamp });
		state.Selection!.StructureId.ShouldBe(structure.Id);
		(await _applicationAppService.GetWorkflowAsync(application.Id)).CurrentStageInstanceId.ShouldBe(before.CurrentStageInstanceId);
		(await GetRequiredService<IAdmissionPaymentAppService>().GetAsync(application.Id, stage.Id)).CanComplete.ShouldBeFalse();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionPaymentTransaction, Guid>>().GetListAsync(item => item.ApplicationId == application.Id))).ShouldBeEmpty();
		await _applicationAppService.EndJourneyAsync(application.Id, new() { Reason = "Withdrawal after fee selection", ConcurrencyStamp = state.ConcurrencyStamp });
		var closed = await service.GetAsync(application.Id);
		closed.CanEdit.ShouldBeFalse();
		closed.Selection!.StructureId.ShouldBe(structure.Id);
	}
}
