using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
    [Fact]
    public async Task Split_Receipt_Amounts_Are_Restored_For_Older_Idempotent_Responses()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var (year, student, head, due, _) = await SeedStudentWithOverdueDueFeeAsync(10, true);
            var command = new CollectFeeInput
            {
                IdempotencyKey = Guid.NewGuid().ToString("N"), StudentId = student, AcademicYearId = year,
                DueFeePayments = [new() { DueFeeId = due, FeeHeadId = head, Amount = 5000d }],
                LateFeePayments = [new() { DueFeeId = due, Amount = 200d }],
                Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 5200d }],
            };
            var result = await _feeCollections.CollectFeeAsync(command);
            result.FeeReceiptAmount.ShouldBe(5000m);
            result.ChargeReceiptAmount.ShouldBe(200m);
            var metadata = GetRequiredService<IRepository<FeeRecordMetadata, Guid>>();
            var stored = await metadata.GetAsync(result.Id);
            var oldResponse = JsonNode.Parse(stored.CommandResultJson!)!.AsObject();
            oldResponse.Remove(nameof(CollectFeeOutput.FeeReceiptAmount));
            oldResponse.Remove(nameof(CollectFeeOutput.ChargeReceiptAmount));
            stored.SetCommandResult(oldResponse.ToJsonString());
            await metadata.UpdateAsync(stored, autoSave: true);

            var replay = await _feeCollections.CollectFeeAsync(command);

            replay.Id.ShouldBe(result.Id);
            replay.FeeReceiptId.ShouldBe(result.FeeReceiptId);
            replay.ChargeReceiptId.ShouldBe(result.ChargeReceiptId);
            replay.FeeReceiptAmount.ShouldBe(5000m);
            replay.ChargeReceiptAmount.ShouldBe(200m);
            var receipts = await GetRequiredService<IFeePaymentReceiptRepository>()
                .GetListAsync(r => r.FeeCollectionTransactionId == result.Id);
            receipts.Count.ShouldBe(2);
            receipts.Sum(r => (decimal)r.TotalPaidAmount).ShouldBe(5200m);
        }
    }
}
