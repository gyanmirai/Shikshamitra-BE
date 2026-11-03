using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Receipts;

public class LateFeeReceiptGrouping_Tests
{
    [Theory]
    [InlineData(CollectionReceiptGrouping.Separate, 20000, 60600, 0, 2)]
    [InlineData(CollectionReceiptGrouping.Separate, 20000, 60600, 400, 2)]
    [InlineData(CollectionReceiptGrouping.Separate, 0, 60600, 400, 1)]
    [InlineData(CollectionReceiptGrouping.Separate, 0, 60600, 0, 1)]
    [InlineData(CollectionReceiptGrouping.Combined, 20000, 60600, 400, 1)]
    public async Task Late_Fees_Are_In_The_Charge_Component(
        CollectionReceiptGrouping grouping, int principal, int late, int charges, int count)
    {
        var total = principal + late + charges;
        var result = await Manager().ExecuteCollectionTransactionAsync(new(
            StudentId: Guid.NewGuid(), AcademicYearId: Guid.NewGuid(), BoardId: Guid.NewGuid(),
            EducationMediumId: Guid.NewGuid(), Status: FeeCollectionStatus.Paid, ReceivedAmount: total,
            Notes: null, BankReceiptConfirmed: null,
            FeeHeadGroups: principal > 0 ? [(Guid.NewGuid(), principal)] : [],
            ChargePayments: charges > 0 ? [(Guid.NewGuid(), charges)] : [],
            DueFeeAllocations: [], Payments: [(PaymentMethodEnum.Cash, total, null)],
            LateFeeAllocations: [(Guid.NewGuid(), late)], ReceiptGrouping: grouping));

        result.Receipts.Count.ShouldBe(count);
        result.Receipts.Sum(r => (decimal)r.TotalPaidAmount).ShouldBe(total);
        result.Receipts.Sum(r => r.FeePaidAmount).ShouldBe(principal);
        result.Receipts.Sum(r => r.ChargePaidAmount).ShouldBe(late + charges);
        result.Receipts.Sum(r => r.LateFeePaidAmount ?? 0m).ShouldBe(late);
        result.FeeLinesTotal.ShouldBe(principal);
        result.ChargeLinesTotal.ShouldBe(charges);
        result.LateFeeLinesTotal.ShouldBe(late);
        result.Receipts.Select(r => r.ReceiptNumber).Distinct().Count().ShouldBe(count);
        if (grouping == CollectionReceiptGrouping.Separate)
        {
            result.Receipts.Single(r => r.ReceiptScope == FeeReceiptScope.Charge)
                .TotalPaidAmount.ShouldBe(late + charges);
            if (principal > 0)
                result.Receipts.Single(r => r.ReceiptScope == FeeReceiptScope.Fee)
                    .TotalPaidAmount.ShouldBe(principal);
        }
    }

    [Fact]
    public void Late_Fee_Snapshot_Is_Immutable_And_Cannot_Exceed_The_Charge_Component()
    {
        var receipt = new FeePaymentReceipt(Guid.NewGuid())
        {
            ReceiptScope = FeeReceiptScope.Charge, TotalPaidAmount = 100
        };
        receipt.LateFeePaidAmount.ShouldBeNull();
        receipt.SetLateFeePaidAmount(40m);
        receipt.SetLateFeePaidAmount(40m);
        receipt.LateFeePaidAmount.ShouldBe(40m);
        Should.Throw<Volo.Abp.BusinessException>(() => receipt.SetLateFeePaidAmount(41m));
        Should.Throw<Volo.Abp.BusinessException>(() => receipt.SetLateFeePaidAmount(-1m));
        Should.Throw<Volo.Abp.BusinessException>(() => receipt.SetLateFeePaidAmount(101m));
        var principal = new FeePaymentReceipt(Guid.NewGuid())
        {
            ReceiptScope = FeeReceiptScope.Fee, TotalPaidAmount = 100
        };
        Should.Throw<Volo.Abp.BusinessException>(() => principal.SetLateFeePaidAmount(1m));
    }

    private static FeeCollectionManager Manager()
    {
        var sequences = Substitute.For<IReceiptNumberSequenceRepository>();
        long sequence = 0;
        sequences.IncrementAndGetAsync(Arg.Any<int>(), Arg.Any<FeeReceiptKind?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref sequence));
        var clock = Substitute.For<IClock>();
        clock.Now.Returns(new DateTime(2026, 10, 30));
        var tenant = Substitute.For<ICurrentTenant>();
        var services = new AbpLazyServiceProvider(new ServiceCollection()
            .AddSingleton<IGuidGenerator>(SimpleGuidGenerator.Instance).BuildServiceProvider());
        var receipts = new FeeReceiptManager(sequences, clock, tenant) { LazyServiceProvider = services };
        return new FeeCollectionManager(
            Substitute.For<IRepository<FeeRecordMetadata, Guid>>(),
            Substitute.For<IRepository<FeeCollectionRecord, Guid>>(),
            Substitute.For<IRepository<ChargeCollectionRecord, Guid>>(),
            Substitute.For<IRepository<PaymentMethodRecord, Guid>>(),
            Substitute.For<IRepository<FeeCollectionDueFeeAllocation, Guid>>(),
            Substitute.For<IRepository<LateFeeCollectionAllocation, Guid>>(),
            Substitute.For<IDueFeeRepository>(), Substitute.For<IRepository<FeeHead, Guid>>(),
            Substitute.For<IFeePaymentReceiptRepository>(), receipts, sequences, clock, tenant,
            Substitute.For<ICurrentUser>()) { LazyServiceProvider = services };
    }
}
