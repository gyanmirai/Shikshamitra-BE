using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Combined_approval_previews_without_writes_and_finalizes_once_without_contact_attestation() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("CombinedReady", completeWorkflow: false);
			var application = await _applicationAppService.GetAsync(fixture.Application.Id);
			application.Status.ShouldBe(AdmissionApplicationStatus.Submitted);
			var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
			runtime.Stages.Single(stage => stage.Id == application.CurrentStageInstanceId).IsTerminalStage.ShouldBeTrue();
			runtime.Stages.Single(stage => stage.StageType == AdmissionWorkflowStageType.Form).IsTerminalStage.ShouldBeFalse();
			var preview = await _applicationAppService.GetFinalizationPreviewAsync(application.Id, forApproval: true);
			preview.IsReady.ShouldBeTrue();
			preview.PrimaryContact.ShouldNotBeNull().Email.ShouldBe("meera.patel@example.test");
			(await _applicationAppService.GetFinalizationPreviewAsync(application.Id)).IsReady.ShouldBeFalse();
			(await _applicationAppService.GetAsync(application.Id)).ConcurrencyStamp.ShouldBe(application.ConcurrencyStamp);
			var command = CombinedFinalization(application, "combined-ready");
			var result = await _applicationAppService.ApproveAndFinalizeAsync(application.Id, command);
			result.Application.Status.ShouldBe(AdmissionApplicationStatus.AdmissionFinalized);
			result.StudentId.ShouldBe(fixture.Applicant.ReservedStudentId);
			(await _applicationAppService.GetWorkflowAsync(application.Id)).Status.ShouldBe(AdmissionWorkflowInstanceStatus.Completed);
			var history = await _applicationAppService.GetDecisionHistoryAsync(application.Id);
			history.Count.ShouldBe(2);
			var replay = await _applicationAppService.ApproveAndFinalizeAsync(application.Id, command);
			replay.StudentId.ShouldBe(result.StudentId);
			replay.OnboardingCaseId.ShouldBe(result.OnboardingCaseId);
			(await _applicationAppService.GetDecisionHistoryAsync(application.Id)).Count.ShouldBe(history.Count);
			await WithUnitOfWorkAsync(async () =>
			{
				(await _onboardingCaseRepository.GetAsync(result.OnboardingCaseId)).GuardianContactVerifiedAt.ShouldBeNull();
				(await _onboardingCaseRepository.CountAsync(item => item.ApplicationId == application.Id)).ShouldBe(1);
				(await _studentEnrollmentRepository.CountAsync(item => item.StudentId == result.StudentId)).ShouldBe(0);
			});
		});

	[Theory]
	[InlineData(false, true)]
	[InlineData(true, false)]
	public Task Combined_approval_blocks_incomplete_identity_or_missing_guardian_without_an_approval(bool completeIdentity, bool createGuardian) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("CombinedBlocked", completeWorkflow: false,
				completeIdentity: completeIdentity, createGuardian: createGuardian);
			var preview = await _applicationAppService.GetFinalizationPreviewAsync(fixture.Application.Id, true);
			preview.IsReady.ShouldBeFalse();
			(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.ApproveAndFinalizeAsync(fixture.Application.Id,
				CombinedFinalization(fixture.Application, "combined-blocked")))).Code.ShouldBe(StudentOnboardingErrorCodes.FinalizationNotReady);
			(await _applicationAppService.GetAsync(fixture.Application.Id)).Status.ShouldBe(AdmissionApplicationStatus.Submitted);
			(await _applicationAppService.GetDecisionHistoryAsync(fixture.Application.Id)).ShouldBeEmpty();
		});

	[Fact]
	public Task Combined_approval_rejects_stale_stamp_and_duplicate_identity_until_resolved() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("CombinedDuplicate", completeWorkflow: false);
			var student = await CreateExistingStudentAsync(fixture.Offering, "CombinedDuplicate", "Patel");
			var command = CombinedFinalization(fixture.Application, "combined-duplicate");
			command.ConcurrencyStamp = "stale";
			(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.ApproveAndFinalizeAsync(fixture.Application.Id, command)))
				.Code.ShouldBe(StudentOnboardingErrorCodes.ConcurrencyConflict);
			command.ConcurrencyStamp = fixture.Application.ConcurrencyStamp;
			command.ConfirmCreateNew = false;
			(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.ApproveAndFinalizeAsync(fixture.Application.Id, command)))
				.Code.ShouldBe(StudentOnboardingErrorCodes.DuplicateResolutionRequired);
			(await _applicationAppService.GetAsync(fixture.Application.Id)).Status.ShouldBe(AdmissionApplicationStatus.Submitted);
			command.ExistingStudentId = student.Id;
			(await _applicationAppService.ApproveAndFinalizeAsync(fixture.Application.Id, command)).StudentId.ShouldBe(student.Id);
		});

	[Fact]
	public Task Combined_approval_cannot_consume_an_unfinished_preceding_stage() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCurrentJourneyApplicationAsync(AdmissionWorkflowStageType.Assessment);
			var application = await _applicationAppService.GetAsync(fixture.Application.Id);
			(await _applicationAppService.GetFinalizationPreviewAsync(application.Id, true)).IsReady.ShouldBeFalse();
			(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.ApproveAndFinalizeAsync(application.Id,
				CombinedFinalization(application, "combined-before-assessment")))).Code.ShouldBe(StudentOnboardingErrorCodes.FinalizationNotReady);
			(await _applicationAppService.GetDecisionHistoryAsync(application.Id)).ShouldBeEmpty();
		});

	[Fact]
	public Task Combined_approval_stage_completion_student_and_case_roll_back_together() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("CombinedRollback", completeWorkflow: false);
			var application = fixture.Application;
			await Should.ThrowAsync<InvalidOperationException>(() => WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				await using var transaction = await db.Database.BeginTransactionAsync();
				try
				{
					var result = await _applicationAppService.ApproveAndFinalizeAsync(application.Id, CombinedFinalization(application, "combined-rollback"));
					(await _onboardingCaseRepository.CountAsync(item => item.ApplicationId == application.Id)).ShouldBe(1);
					throw new InvalidOperationException("Failure after all combined writes were saved.");
				}
				finally { await transaction.RollbackAsync(); }
			}));
			var stored = await _applicationAppService.GetAsync(application.Id);
			stored.Status.ShouldBe(AdmissionApplicationStatus.Submitted);
			stored.CurrentStageInstanceId.ShouldBe(application.CurrentStageInstanceId);
			(await _applicationAppService.GetWorkflowAsync(application.Id)).Status.ShouldBe(AdmissionWorkflowInstanceStatus.Active);
			(await _applicationAppService.GetDecisionHistoryAsync(application.Id)).ShouldBeEmpty();
			await WithUnitOfWorkAsync(async () =>
			{
				(await _onboardingCaseRepository.CountAsync(item => item.ApplicationId == application.Id)).ShouldBe(0);
				(await _studentRepository.FindAsync(fixture.Applicant.ReservedStudentId)).ShouldBeNull();
			});
		});

	[Fact]
	public Task Combined_approval_cannot_treat_structured_parent_answers_as_a_missing_primary_guardian() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("CombinedParentOnly", completeWorkflow: false, createGuardian: false);
			await WithUnitOfWorkAsync(async () =>
			{
				var applicant = await _applicantRepository.GetAsync(fixture.Applicant.Id);
				AdmissionPrimaryContactSelection.Capture(applicant, "mother", "Actual Mother", "9876543210", "mother@example.test",
					false, TestUserId, DateTime.UtcNow, isPublic: true);
				await _applicantRepository.UpdateAsync(applicant, true);
			});
			var preview = await _applicationAppService.GetFinalizationPreviewAsync(fixture.Application.Id, true);
			preview.PrimaryContact.ShouldNotBeNull().Name.ShouldBe("Actual Mother");
			preview.Items.ShouldContain(item => item.Key == "guardian" && !item.Passed);
			(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.ApproveAndFinalizeAsync(fixture.Application.Id,
				CombinedFinalization(fixture.Application, "combined-missing-guardian")))).Code.ShouldBe(StudentOnboardingErrorCodes.FinalizationNotReady);
			(await _applicationAppService.GetDecisionHistoryAsync(fixture.Application.Id)).ShouldBeEmpty();
		});

	[Fact]
	public Task Combined_approval_does_not_consume_a_nonterminal_approval_with_a_following_route() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("CombinedRouted", completeWorkflow: false);
			await WithUnitOfWorkAsync(async () =>
			{
				var stages = GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>();
				var current = await stages.GetAsync(fixture.Application.CurrentStageInstanceId!.Value);
				var definitions = GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>();
				var definition = await definitions.GetAsync(current.DefinitionStageId);
				definition.Update(definition.Code, definition.Name, definition.StageType, definition.DisplayOrder,
					definition.IsRequired, definition.AllowManualSkip, definition.Description, definition.ConfigurationJson, definition.IsEntryStage, false);
				await definitions.UpdateAsync(definition, true);
				var next = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), definition.WorkflowDefinitionId, "LATER", "Later fees",
					AdmissionWorkflowStageType.Payment, 3, true, false, null, null, false, true);
				await definitions.InsertAsync(next, true);
				await stages.InsertAsync(new AdmissionApplicationStageInstance(Guid.NewGuid(), TestTenantId, current.WorkflowInstanceId,
					next.Id, next.Code, next.Name, next.StageType, next.DisplayOrder, false), true);
				await GetRequiredService<IRepository<AdmissionWorkflowTransitionDefinition, Guid>>().InsertAsync(new(
					Guid.NewGuid(), definition.WorkflowDefinitionId, definition.Id, next.Id, 1, "{\"field\":\"decision.status\",\"operator\":\"equals\",\"value\":\"Approved\"}"), true);
			});
			var runtime = await _applicationAppService.GetWorkflowAsync(fixture.Application.Id);
			runtime.Stages.Single(stage => stage.Id == fixture.Application.CurrentStageInstanceId).IsTerminalStage.ShouldBeFalse();
			(await _applicationAppService.GetFinalizationPreviewAsync(fixture.Application.Id, true)).IsReady.ShouldBeFalse();
			(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.ApproveAndFinalizeAsync(fixture.Application.Id,
				CombinedFinalization(fixture.Application, "combined-nonterminal")))).Code.ShouldBe(StudentOnboardingErrorCodes.FinalizationNotReady);
			(await _applicationAppService.GetDecisionHistoryAsync(fixture.Application.Id)).ShouldBeEmpty();
		});

	[Fact]
	public Task Combined_approval_finance_handoff_rolls_back_and_preserves_the_original_payment_on_retry() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCollectibleAdmissionAsync("CombinedFinance");
			var finance = GetRequiredService<IAdmissionFeeCollectionAppService>();
			var prepared = await finance.PrepareAsync(fixture.Application.Id, new());
			var accountId = prepared.AccountId!.Value;
			var due = (await AccountDuesAsync(accountId)).Single();
			await finance.CollectAsync(fixture.Application.Id, AdmissionCashPayment(due, 1000m, "combined-finance-payment"));
			var paymentStage = (await _applicationAppService.GetWorkflowAsync(fixture.Application.Id)).CurrentStageInstanceId!.Value;
			await _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id, new()
			{
				ExpectedCurrentStageInstanceId = paymentStage,
				ConcurrencyStamp = (await _applicationAppService.GetAsync(fixture.Application.Id)).ConcurrencyStamp,
				IdempotencyKey = "combined-finance-payment-complete"
			});
			var application = await _applicationAppService.GetAsync(fixture.Application.Id);
			var accounts = GetRequiredService<IRepository<FinanceFeeAccount, Guid>>();
			await Should.ThrowAsync<InvalidOperationException>(() => WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				await using var transaction = await db.Database.BeginTransactionAsync();
				try
				{
					var result = await _applicationAppService.ApproveAndFinalizeAsync(application.Id, CombinedFinalization(application, "combined-finance-finalize"));
					(await accounts.GetAsync(accountId)).StudentId.ShouldBe(result.StudentId);
					throw new InvalidOperationException("Failure after Finance linked the Student.");
				}
				finally { await transaction.RollbackAsync(); }
			}));
			(await WithUnitOfWorkAsync(() => accounts.GetAsync(accountId))).StudentId.ShouldBeNull();
			(await _applicationAppService.GetAsync(application.Id)).Status.ShouldBe(AdmissionApplicationStatus.Submitted);
			(await _applicationAppService.GetDecisionHistoryAsync(application.Id)).ShouldBeEmpty();
			var result = await _applicationAppService.ApproveAndFinalizeAsync(application.Id, CombinedFinalization(application, "combined-finance-finalize"));
			(await WithUnitOfWorkAsync(() => accounts.GetAsync(accountId))).StudentId.ShouldBe(result.StudentId);
			var retainedDue = (await AccountDuesAsync(accountId)).Single();
			retainedDue.Id.ShouldBe(due.Id);
			var summary = await finance.GetSummaryAsync(application.Id);
			summary.ConfirmedPaidAmount.ShouldBe(1000m);
			summary.OutstandingAmount.ShouldBe(0m);
			summary.IsSettled.ShouldBeTrue();
		});

	private static ApproveAndFinalizeAdmissionApplicationDto CombinedFinalization(AdmissionApplicationDto application, string key) => new()
	{
		ConcurrencyStamp = application.ConcurrencyStamp, IdempotencyKey = key, ConfirmCreateNew = true
	};
}
