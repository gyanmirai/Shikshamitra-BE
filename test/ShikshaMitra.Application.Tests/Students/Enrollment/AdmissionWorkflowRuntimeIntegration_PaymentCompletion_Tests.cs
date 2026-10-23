using System;
using System.Linq;
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
    public Task Payment_completion_enforces_published_rule_against_actual_Finance_collection(string rule) =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("PaymentRule", completion:
                new() { PaymentRule = rule, AllowCollectionLater = false });
            var fees = GetRequiredService<IAdmissionFeeSelectionAppService>();
            var state = await fees.GetAsync(fixture.Application.Id);
            var stage = state.CurrentStageId!.Value;
            state.PaymentCompletion!.PaymentRule.ShouldBe(rule);
            state.PaymentCompletion.CanComplete.ShouldBeFalse();
            state.PaymentCompletion.CanDeferCollection.ShouldBeFalse();
            var blocked = await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id,
                new() { ExpectedCurrentStageInstanceId = stage, ConcurrencyStamp = state.ConcurrencyStamp,
                    IdempotencyKey = "no-payment-bypass", DeferFeeCollection = true, Reason = "Try bypass" }));
            blocked.Code.ShouldBe(AdmissionFeeSelectionErrorCodes.CollectionOrDeferralRequired);
            (await fees.GetAsync(fixture.Application.Id)).FeeCollectionDeferral.ShouldBeNull();
            (await AccountCountAsync(fixture.Application.Id)).ShouldBe(0);
            var finance = GetRequiredService<IAdmissionFeeCollectionAppService>();
            var prepared = await finance.PrepareAsync(fixture.Application.Id, new());
            var due = (await AccountDuesAsync(prepared.AccountId!.Value)).Single();
            await finance.CollectAsync(fixture.Application.Id, AdmissionCashPayment(due, 300m, "rule-partial"));
            state = await fees.GetAsync(fixture.Application.Id);
            state.PaymentCompletion!.CanComplete.ShouldBe(rule == "partial");
            if (rule == "full")
            {
                (await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id,
                    new() { ExpectedCurrentStageInstanceId = stage, ConcurrencyStamp = state.ConcurrencyStamp,
                        IdempotencyKey = "partial-not-full" }))).Code.ShouldBe(AdmissionFeeSelectionErrorCodes.CollectionOrDeferralRequired);
                await finance.CollectAsync(fixture.Application.Id, AdmissionCashPayment(due, 700m, "rule-rest"));
                state = await fees.GetAsync(fixture.Application.Id);
                state.PaymentCompletion!.Status.ShouldBe("financeCleared");
                state.PaymentCompletion.CanComplete.ShouldBeTrue();
            }
            var command = new AdvanceAdmissionWorkflowDto { ExpectedCurrentStageInstanceId = stage,
                ConcurrencyStamp = state.ConcurrencyStamp, IdempotencyKey = "rule-continue" };
            var next = await _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id, command);
            next.CurrentStageInstanceId.ShouldNotBe(stage);
            next.Stages.Single(s => s.Id == stage).Status.ShouldBe(AdmissionRuntimeStageStatus.Completed);
            (await _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id, command)).CurrentStageInstanceId.ShouldBe(next.CurrentStageInstanceId);
            (await fees.GetAsync(fixture.Application.Id)).FeeCollectionDeferral.ShouldBeNull();
        });

    [Fact]
    public Task Payment_completion_accepts_Finance_verified_zero_without_creating_payment_evidence() =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("ZeroPayment", completion:
                new() { PaymentRule = "full", AllowCollectionLater = false }, amount: 0m);
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
}
