using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Admissions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task New_Form_defaults_advance_on_submit_but_required_verification_holds(bool requireVerification)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Five model Form QA"), true);
		var draft = await _workflowAppService.CreateAsync(new() { Name = "Managed Form", Code = $"FORM_{Guid.NewGuid():N}"[..24],
			Stages = [new() { Code = "FORM", Name = "Application form", StageType = AdmissionWorkflowStageType.Form,
				IsEntryStage = true, IsTerminalStage = true, IsRequired = false, AllowManualSkip = true }] });
		var offering = await CreateAvailableOfferingAsync(draft.Id, assertValid: false);
		var stageId = draft.Stages.Single().Id;
		draft = await _workflowAppService.UpdateAsync(draft.Id, new() { Name = draft.Name, Code = draft.Code, ConcurrencyStamp = draft.ConcurrencyStamp,
			Stages = [new() { Id = stageId, Code = "FORM", Name = "Application form", StageType = AdmissionWorkflowStageType.Form,
				IsEntryStage = true, IsTerminalStage = true, IsRequired = false, AllowManualSkip = true,
				ConfigurationJson = JsonSerializer.Serialize(new { formDefinitionId = offering.ApplicationFormDefinitionId,
					form = new AdmissionFormStagePolicy { Enabled = true, Version = 1, AutoAdvanceOnSubmit = false,
						RequireSectionVerification = requireVerification, VerificationSectionCodes = requireVerification ? ["STUDENT"] : [] } }) }] });
		await _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp);
		draft.Stages.Single().AllowManualSkip.ShouldBeFalse();
		var application = await StartFixtureApplicationAsync((await CreateApplicantAsync("Form QA")).Id, offering.Id);
		var before = await _applicationAppService.GetWorkflowAsync(application.Id);
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(application.Id,
			new() { ExpectedCurrentStageInstanceId = before.CurrentStageInstanceId!.Value, ConcurrencyStamp = application.ConcurrencyStamp,
				SkipCurrentStage = true, Reason = "Ordinary skip is unavailable", IdempotencyKey = "no-form-bypass" })))
			.Code.ShouldBe(AdmissionApplicationErrorCodes.ManualSkipNotAllowed);
		(await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
		var after = await _applicationAppService.GetWorkflowAsync(application.Id);
		if (!requireVerification) { after.Status.ShouldBe(AdmissionWorkflowInstanceStatus.Completed); return; }
		after.CurrentStageInstanceId.ShouldBe(before.CurrentStageInstanceId);
		var service = GetRequiredService<IAdmissionFormStageAppService>();
		var pending = await service.GetAsync(application.Id, after.CurrentStageInstanceId!.Value);
		pending.CanComplete.ShouldBeFalse(); pending.Policy!.AutoAdvanceOnSubmit.ShouldBeTrue();
		var verified = await service.VerifySectionAsync(application.Id, after.CurrentStageInstanceId.Value, pending.Sections.Single(item => item.Required).SectionId,
			new() { ConcurrencyStamp = pending.ConcurrencyStamp, Verified = true });
		verified.CanComplete.ShouldBeTrue();
		(await _applicationAppService.AdvanceWorkflowAsync(application.Id, new() { ExpectedCurrentStageInstanceId = after.CurrentStageInstanceId.Value,
			ConcurrencyStamp = verified.ConcurrencyStamp, IdempotencyKey = "complete-verified-form" })).Status.ShouldBe(AdmissionWorkflowInstanceStatus.Completed);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Payment_deferral_advances_with_or_without_structure_and_retry_and_finalization_preserve_reason_without_finance_rows(bool selectStructure)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Five stage QA"), true);
		var payment = Guid.NewGuid(); var decision = Guid.NewGuid();
		var workflow = await _workflowAppService.CreateAsync(new()
		{
			Name = "Fee confirmation", Code = $"FIVE_{Guid.NewGuid():N}"[..24],
			Stages = [new() { Id = payment, Code = "FEES", Name = "Confirm fee structure", StageType = AdmissionWorkflowStageType.Payment,
				IsEntryStage = true, IsRequired = true }, new() { Id = decision, Code = "DECISION", Name = "Admission decision",
				StageType = AdmissionWorkflowStageType.Approval, DisplayOrder = 1, IsTerminalStage = true, IsRequired = true }],
			Transitions = [new() { FromStageId = payment, ToStageId = decision, IsDefault = true, Priority = 1 }]
		});
		workflow = await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
		var offering = await CreateAvailableOfferingAsync(workflow.Id);
		var applicant = await CreateApplicantAsync("Fee Confirm QA");
		await WithUnitOfWorkAsync(async () =>
		{
			var family = await _familyRepository.GetAsync(applicant.FamilyId);
			var guardian = new AdmissionGuardian(Guid.NewGuid(), TestTenantId, family.Id, "QA", "Guardian", "9876543210", null, AdmissionGuardianRelationship.Parent, true);
			await _guardianRepository.InsertAsync(guardian, true); family.SetPrimaryGuardian(guardian.Id);
			await _familyRepository.UpdateAsync(family, true);
		});
		var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
		var fees = GetRequiredService<IAdmissionFeeSelectionAppService>();
		var state = await fees.GetAsync(application.Id); state.IsRequired.ShouldBeTrue(); state.CanEdit.ShouldBeTrue();
		var stage = state.CurrentStageId!.Value;
		var dueCount = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<DueFee, Guid>>().CountAsync());
		var assignmentCount = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentFeeStructure, Guid>>().CountAsync());
		var accountCount = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().CountAsync());
		var collectionCount = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FeeCollectionRecord, Guid>>().CountAsync());
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.MarkInReviewAsync(application.Id,
			new() { ConcurrencyStamp = application.ConcurrencyStamp }))).Code.ShouldBe(AdmissionReviewErrorCodes.InvalidState);
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.IssueOfferAsync(application.Id,
			new() { ConcurrencyStamp = application.ConcurrencyStamp }))).Code.ShouldBe(AdmissionOfferStageErrorCodes.InvalidState);
		(await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => GetRequiredService<AdmissionEligibilityAccessService>().GetContextAsync(application.Id, stage))))
			.Code.ShouldBe(AdmissionEligibilityErrorCodes.InvalidState);
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.ApproveAsync(application.Id,
			new() { ConcurrencyStamp = application.ConcurrencyStamp }))).Code.ShouldBe(AdmissionErrorCodes.ApplicationDecisionInvalidState);
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(application.Id,
			new() { ExpectedCurrentStageInstanceId = stage, ConcurrencyStamp = state.ConcurrencyStamp, IdempotencyKey = "missing-fees" })))
			.Code.ShouldBe(AdmissionFeeSelectionErrorCodes.CollectionOrDeferralRequired);
		if (selectStructure)
		{
			var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
			var structure = await CreateFeeSelectionReferenceAsync(offering, cycle.AcademicYearId);
			state = await fees.SaveAsync(application.Id, new() { StructureId = structure.Id, ConcurrencyStamp = state.ConcurrencyStamp });
			(await fees.GetAsync(application.Id)).Selection!.StructureId.ShouldBe(structure.Id);
		}
		var reader = GetRequiredService<IAdmissionPaymentAppService>();
		var confirmed = await reader.GetAsync(application.Id, stage);
		confirmed.Status.ShouldBe(selectStructure ? "collectionPending" : "feeStructureRequired"); confirmed.CanRecord.ShouldBeFalse(); confirmed.CanComplete.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => reader.RecordAsync(application.Id, stage, new()
			{ Amount = 200m, Method = "cash", IdempotencyKey = "no-collection", ConcurrencyStamp = state.ConcurrencyStamp })))
			.Code.ShouldBe(AdmissionPaymentStageErrorCodes.InvalidState);
		var command = new AdvanceAdmissionWorkflowDto { ExpectedCurrentStageInstanceId = stage,
			ConcurrencyStamp = state.ConcurrencyStamp, IdempotencyKey = "defer-fees", DeferFeeCollection = true,
			Reason = "  Parent will pay during Onboarding.  " };
		await Should.ThrowAsync<AbpValidationException>(() => _applicationAppService.AdvanceWorkflowAsync(application.Id,
			new() { ExpectedCurrentStageInstanceId = stage, ConcurrencyStamp = state.ConcurrencyStamp,
				IdempotencyKey = "blank-deferral", DeferFeeCollection = true, Reason = "   " }));
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(application.Id,
			new() { ExpectedCurrentStageInstanceId = stage, ConcurrencyStamp = "stale",
				IdempotencyKey = "stale-deferral", DeferFeeCollection = true, Reason = "Collect later" })))
			.Code.ShouldBe(AdmissionApplicationErrorCodes.ConcurrencyConflict);
		var next = await _applicationAppService.AdvanceWorkflowAsync(application.Id, command);
		(await _applicationAppService.AdvanceWorkflowAsync(application.Id, command)).CurrentStageInstanceId.ShouldBe(next.CurrentStageInstanceId);
		next.Stages.Single(s => s.Id == stage).Status.ShouldBe(AdmissionRuntimeStageStatus.Completed);
		next.History.Single(h => h.FromStageInstanceId == stage).ConditionEvaluationSnapshotJson.ShouldContain("collectionDeferred");
		next.History.Single(h => h.FromStageInstanceId == stage).ConditionEvaluationSnapshotJson.ShouldNotContain("feeStructureConfirmed");
		var deferred = (await fees.GetAsync(application.Id)).FeeCollectionDeferral!;
		var nextConcurrencyStamp = (await fees.GetAsync(application.Id)).ConcurrencyStamp;
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(application.Id,
			new() { ExpectedCurrentStageInstanceId = next.CurrentStageInstanceId!.Value,
				ConcurrencyStamp = nextConcurrencyStamp,
				IdempotencyKey = "deferral-wrong-stage", DeferFeeCollection = true, Reason = "Collect later" })))
			.Code.ShouldBe(AdmissionFeeSelectionErrorCodes.InvalidState);
		deferred.Reason.ShouldBe("Parent will pay during Onboarding."); deferred.DeferredBy.ShouldBe(TestUserId);
		deferred.StageId.ShouldBe(stage); deferred.DeferredAt.Offset.ShouldBe(TimeSpan.Zero);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionActivity, Guid>>().CountAsync(a =>
			a.ApplicationId == application.Id && a.ActivityType == "FeeCollectionDeferred"))).ShouldBe(1);
		(await GetRequiredService<IRepository<AdmissionPaymentTransaction, Guid>>().GetListAsync(item => item.ApplicationId == application.Id)).ShouldBeEmpty();
		await WithUnitOfWorkAsync(async () => { var stored = await _applicationRepository.GetAsync(application.Id); stored.Submit(); await _applicationRepository.UpdateAsync(stored, true); });
		var current = await _applicationAppService.GetAsync(application.Id);
		var waitlisted = await _applicationAppService.WaitlistAsync(application.Id, new() { ConcurrencyStamp = current.ConcurrencyStamp, Reason = "Await decision" });
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(application.Id,
			new() { ExpectedCurrentStageInstanceId = next.CurrentStageInstanceId!.Value, ConcurrencyStamp = waitlisted.ConcurrencyStamp, IdempotencyKey = "waitlist-is-pending" })))
			.Code.ShouldBe(AdmissionApplicationErrorCodes.StageCannotAdvance);
		var approved = await _applicationAppService.ApproveAsync(application.Id, new() { ConcurrencyStamp = waitlisted.ConcurrencyStamp });
		await _applicationAppService.AdvanceWorkflowAsync(application.Id, new() { ExpectedCurrentStageInstanceId = next.CurrentStageInstanceId!.Value,
			ConcurrencyStamp = approved.ConcurrencyStamp, IdempotencyKey = "complete-decision" });
		var preview = await _applicationAppService.GetFinalizationPreviewAsync(application.Id);
		preview.Items.ShouldContain(item => item.Key == "decision" && item.Passed);
		preview.Items.ShouldNotContain(item => item.Key == "offer");
		current = await _applicationAppService.GetAsync(application.Id);
		var finalInput = new FinalizeAdmissionApplicationDto { ConcurrencyStamp = current.ConcurrencyStamp,
			ConfirmGuardianContactVerified = true, IdempotencyKey = "finalize-five-model" };
		var finalized = await _applicationAppService.FinalizeAsync(application.Id, finalInput);
		(await _applicationAppService.FinalizeAsync(application.Id, finalInput)).StudentId.ShouldBe(finalized.StudentId);
		(await _applicationRepository.GetAsync(application.Id)).OfferAcceptedAt.ShouldBeNull();
		var onboardingFees = await GetRequiredService<IStudentOnboardingAppService>().GetFeeStructureAsync(finalized.OnboardingCaseId);
		onboardingFees.FeeCollectionDeferral!.Reason.ShouldBe(deferred.Reason);
		onboardingFees.HasRecordedFeePayment.ShouldBeFalse(); onboardingFees.OnboardingConfirmed.ShouldBeFalse();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<DueFee, Guid>>().CountAsync())).ShouldBe(dueCount);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentFeeStructure, Guid>>().CountAsync())).ShouldBe(assignmentCount);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().CountAsync())).ShouldBe(accountCount);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FeeCollectionRecord, Guid>>().CountAsync())).ShouldBe(collectionCount);
	}
}
