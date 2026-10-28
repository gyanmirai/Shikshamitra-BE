using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
    [Theory]
    [InlineData("partial")]
    [InlineData("full")]
    public Task Payment_checkpoint_accepts_confirmed_partial_payment_despite_retired_published_settings(string oldRule) =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("PaymentCheckpoint");
            var fees = GetRequiredService<IAdmissionFeeSelectionAppService>();
            var state = await fees.GetAsync(fixture.Application.Id);
            var stage = state.CurrentStageId!.Value;
            await SetRetiredPaymentSettingsAsync(stage, oldRule);
            state = await fees.GetAsync(fixture.Application.Id);
            state.PaymentCompletion!.CanComplete.ShouldBeFalse();
            state.PaymentCompletion.CanDeferCollection.ShouldBeTrue();
            (await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id,
                new() { ExpectedCurrentStageInstanceId = stage, ConcurrencyStamp = state.ConcurrencyStamp,
                    IdempotencyKey = "unpaid-still-blocked" }))).Code.ShouldBe(AdmissionFeeSelectionErrorCodes.CollectionOrDeferralRequired);
            (await AccountCountAsync(fixture.Application.Id)).ShouldBe(0);
            var finance = GetRequiredService<IAdmissionFeeCollectionAppService>();
            var prepared = await finance.PrepareAsync(fixture.Application.Id, new());
            var due = (await AccountDuesAsync(prepared.AccountId!.Value)).Single();
            await finance.CollectAsync(fixture.Application.Id, AdmissionCashPayment(due, 300m, "checkpoint-partial"));
            state = await fees.GetAsync(fixture.Application.Id);
            state.PaymentCompletion!.CanComplete.ShouldBeTrue();
            (await finance.GetSummaryAsync(fixture.Application.Id)).OutstandingAmount.ShouldBe(700m);
            var command = new AdvanceAdmissionWorkflowDto { ExpectedCurrentStageInstanceId = stage,
                ConcurrencyStamp = state.ConcurrencyStamp, IdempotencyKey = "checkpoint-continue" };
            var next = await _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id, command);
            next.CurrentStageInstanceId.ShouldNotBe(stage);
            next.Stages.Single(s => s.Id == stage).Status.ShouldBe(AdmissionRuntimeStageStatus.Completed);
            (await _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id, command)).CurrentStageInstanceId.ShouldBe(next.CurrentStageInstanceId);
            (await fees.GetAsync(fixture.Application.Id)).FeeCollectionDeferral.ShouldBeNull();
        });

    [Fact]
    public Task Payment_checkpoint_allows_audited_deferral_despite_retired_disabled_setting() =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("RetiredDeferral");
            var fees = GetRequiredService<IAdmissionFeeSelectionAppService>();
            var state = await fees.GetAsync(fixture.Application.Id);
            var stage = state.CurrentStageId!.Value;
            await SetRetiredPaymentSettingsAsync(stage, "full");
            state = await fees.GetAsync(fixture.Application.Id);
            state.PaymentCompletion!.CanDeferCollection.ShouldBeTrue();
            var command = new AdvanceAdmissionWorkflowDto { ExpectedCurrentStageInstanceId = stage,
                ConcurrencyStamp = state.ConcurrencyStamp, IdempotencyKey = "retired-deferral",
                DeferFeeCollection = true, Reason = "Parent will pay during Onboarding." };
            var next = await _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id, command);
            (await _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id, command)).CurrentStageInstanceId.ShouldBe(next.CurrentStageInstanceId);
            var reloaded = await fees.GetAsync(fixture.Application.Id);
            reloaded.FeeCollectionDeferral!.Reason.ShouldBe(command.Reason);
            reloaded.PaymentCompletion!.CanDeferCollection.ShouldBeFalse();
            (await AccountCountAsync(fixture.Application.Id)).ShouldBe(0);
            (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionActivity, Guid>>()
                .CountAsync(a => a.ApplicationId == fixture.Application.Id && a.ActivityType == "FeeCollectionDeferred"))).ShouldBe(1);
        });

    [Fact]
    public Task Payment_checkpoint_accepts_Finance_verified_zero_without_creating_payment_evidence() =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("ZeroPayment", amount: 0m);
            var fees = GetRequiredService<IAdmissionFeeSelectionAppService>();
            (await fees.GetAsync(fixture.Application.Id)).PaymentCompletion!.CanComplete.ShouldBeFalse();
            var prepared = await GetRequiredService<IAdmissionFeeCollectionAppService>().PrepareAsync(fixture.Application.Id, new());
            prepared.IsSettled.ShouldBeTrue();
            var state = await fees.GetAsync(fixture.Application.Id);
            state.HasRecordedFeePayment.ShouldBeFalse();
            state.PaymentCompletion!.CanComplete.ShouldBeTrue();
            state.PaymentCompletion.Status.ShouldBe("financeCleared");
            await _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id, new()
            { ExpectedCurrentStageInstanceId = state.CurrentStageId!.Value, ConcurrencyStamp = state.ConcurrencyStamp, IdempotencyKey = "zero-continue" });
            (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FeeRecordMetadata, Guid>>()
                .CountAsync(t => t.FinanceFeeAccountId == prepared.AccountId))).ShouldBe(0);
        });

    private Task SetRetiredPaymentSettingsAsync(Guid stageId, string oldRule) => WithUnitOfWorkAsync(async () =>
    {
        // Simulate an already published/pinned workflow. Runtime must ignore the old JSON
        // without rewriting published definitions or applicant history.
        var stage = await GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>().GetAsync(stageId);
        var repository = GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>();
        var definition = await repository.GetAsync(stage.DefinitionStageId);
        var legacy = JsonSerializer.Serialize(new { paymentCompletion = new { version = 1, paymentRule = oldRule, allowCollectionLater = false } });
        definition.Update(definition.Code, definition.Name, definition.StageType, definition.DisplayOrder,
            definition.IsRequired, definition.AllowManualSkip, definition.Description, legacy, definition.IsEntryStage, definition.IsTerminalStage);
        await repository.UpdateAsync(definition, true);
    });
}
