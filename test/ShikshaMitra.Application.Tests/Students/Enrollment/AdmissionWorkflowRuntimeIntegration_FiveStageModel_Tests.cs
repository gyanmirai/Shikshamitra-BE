using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Admissions;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
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

	[Fact]
	public async Task Fee_structure_confirmation_advances_without_any_payment_and_retry_is_idempotent()
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
			.Code.ShouldBe(AdmissionFeeSelectionErrorCodes.Required);
		var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
		var structure = await CreateFeeSelectionReferenceAsync(offering, cycle.AcademicYearId);
		state = await fees.SaveAsync(application.Id, new() { StructureId = structure.Id, ConcurrencyStamp = state.ConcurrencyStamp });
		(await fees.GetAsync(application.Id)).Selection!.StructureId.ShouldBe(structure.Id);
		var reader = GetRequiredService<IAdmissionPaymentAppService>();
		var confirmed = await reader.GetAsync(application.Id, stage);
		confirmed.Status.ShouldBe("feeStructureConfirmed"); confirmed.CanRecord.ShouldBeFalse(); confirmed.CanComplete.ShouldBeTrue();
		(await Should.ThrowAsync<BusinessException>(() => reader.RecordAsync(application.Id, stage, new()
			{ Amount = 200m, Method = "cash", IdempotencyKey = "no-collection", ConcurrencyStamp = state.ConcurrencyStamp })))
			.Code.ShouldBe(AdmissionPaymentStageErrorCodes.InvalidState);
		var command = new AdvanceAdmissionWorkflowDto { ExpectedCurrentStageInstanceId = stage,
			ConcurrencyStamp = state.ConcurrencyStamp, IdempotencyKey = "confirm-fees" };
		var next = await _applicationAppService.AdvanceWorkflowAsync(application.Id, command);
		(await _applicationAppService.AdvanceWorkflowAsync(application.Id, command)).CurrentStageInstanceId.ShouldBe(next.CurrentStageInstanceId);
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
	}
}
