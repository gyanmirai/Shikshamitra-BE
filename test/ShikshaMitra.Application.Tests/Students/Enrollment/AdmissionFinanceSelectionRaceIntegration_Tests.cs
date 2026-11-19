using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
    [Fact]
    public Task Admission_collector_should_reject_selection_changed_after_outer_validation_without_payment_evidence() =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("SelectionRace");
            var admissions = GetRequiredService<IAdmissionFeeCollectionAppService>();
            var prepared = await admissions.PrepareAsync(fixture.Application.Id, new());
            var accountId = prepared.AccountId!.Value;
            var due = (await AccountDuesAsync(accountId)).Single();
            var form = await admissions.GetCollectFormAsync(fixture.Application.Id);
            form.Ledger.SelectMany(m => m.Items).Single().DueFeeId.ShouldBe(due.Id);
            var account = await ReadAsync<FinanceFeeAccount>(accountId);
            var validatedCommand = AdmissionFeeCollectionInputMapper.Map(
                AdmissionCashPayment(due, 300m, "selection-race-payment"), account);

            // Simulate the boundary between the outer Admissions check and the
            // canonical collector's new transaction. The edit uses the real public
            // selection service and a published replacement, without bypassing its guards.
            var replacement = await CreateCollectibleRevisionAsync(fixture, 1200m);
            var selections = GetRequiredService<IAdmissionFeeSelectionAppService>();
            var current = await selections.GetAsync(fixture.Application.Id);
            await selections.SaveAsync(fixture.Application.Id,
                new() { StructureId = replacement.Id, ConcurrencyStamp = current.ConcurrencyStamp });

            var failure = await Should.ThrowAsync<BusinessException>(() =>
                GetRequiredService<CollectFeeWorkflow>().CollectFeeAsync(validatedCommand));
            failure.Code.ShouldBe(AdmissionFeeCollectionErrorCodes.SelectionChanged);
            var unchanged = (await AccountDuesAsync(accountId)).Single();
            unchanged.Id.ShouldBe(due.Id);
            unchanged.PaidAmount.ShouldBe(0m);
            unchanged.OutstandingAmount.ShouldBe(1000m);
            (await ReadAsync<FinanceFeeAccount>(accountId)).HasPaymentEvidence.ShouldBeFalse();
            (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FeeRecordMetadata, Guid>>()
                .CountAsync(m => m.FinanceFeeAccountId == accountId))).ShouldBe(0);
            (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<PaymentMethodRecord, Guid>>()
                .CountAsync(p => p.FinanceFeeAccountId == accountId))).ShouldBe(0);
            (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FeePaymentReceipt, Guid>>()
                .CountAsync(r => r.FinanceFeeAccountId == accountId))).ShouldBe(0);
            (await selections.GetAsync(fixture.Application.Id)).Selection!.StructureId.ShouldBe(replacement.Id);
        });
}
