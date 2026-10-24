using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Collections;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Receipts;

public class CollectionReceiptGrouping_Tests
{
	[Theory]
	[InlineData(CollectionReceiptGrouping.Combined, 1)]
	[InlineData(CollectionReceiptGrouping.Separate, 2)]
	public async Task Mixed_Collection_Preserves_Exact_Total_And_Document_Count(CollectionReceiptGrouping grouping, int count)
	{
		var receipts = await Manager().CreateReceiptsForTransactionAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), 100.10d, 25.20d, [PaymentMethodEnum.Cheque], receiptGrouping: grouping);
		receipts.Count.ShouldBe(count);
		receipts.Sum(r => (decimal)r.TotalPaidAmount).ShouldBe(125.30m);
		receipts.All(r => r.VerificationStatus == FeeReceiptVerificationStatus.PendingVerification).ShouldBeTrue();
		receipts.Select(r => r.ReceiptNumber).Distinct().Count().ShouldBe(count);
		if (grouping == CollectionReceiptGrouping.Combined)
		{
			receipts[0].ReceiptScope.ShouldBe(FeeReceiptScope.Combined);
			receipts[0].FeePaidAmount.ShouldBe(100.10m);
			receipts[0].ChargePaidAmount.ShouldBe(25.20m);
		}
	}

	[Theory]
	[InlineData(CollectionReceiptGrouping.Combined, 100d, 0d, FeeReceiptScope.Fee)]
	[InlineData(CollectionReceiptGrouping.Combined, 0d, 100d, FeeReceiptScope.Charge)]
	[InlineData(CollectionReceiptGrouping.Separate, 100d, 0d, FeeReceiptScope.Fee)]
	[InlineData(CollectionReceiptGrouping.Separate, 0d, 100d, FeeReceiptScope.Charge)]
	public async Task Single_Component_Always_Produces_One_Receipt(CollectionReceiptGrouping grouping, double fees, double charges, FeeReceiptScope scope)
	{
		var receipts = await Manager().CreateReceiptsForTransactionAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), fees, charges, [PaymentMethodEnum.Cash], receiptGrouping: grouping);
		receipts.Count.ShouldBe(1);
		receipts[0].ReceiptScope.ShouldBe(scope);
		receipts[0].VerificationStatus.ShouldBe(FeeReceiptVerificationStatus.NotApplicable);
	}

	[Theory]
	[InlineData(CollectionReceiptGrouping.Combined)]
	[InlineData(CollectionReceiptGrouping.Separate)]
	public async Task Unconfirmed_Bank_Payment_Makes_All_Documents_Provisional(CollectionReceiptGrouping grouping)
	{
		var receipts = await Manager().CreateReceiptsForTransactionAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), 100d, 25d, [PaymentMethodEnum.UPI], bankReceiptConfirmed: false, receiptGrouping: grouping);
		receipts.All(r => r.VerificationStatus == FeeReceiptVerificationStatus.PendingVerification).ShouldBeTrue();
	}

	[Fact]
	public void Policy_Version_Preserves_Grouping_And_Old_Snapshots_Keep_Separate_Receipts()
	{
		var model = new CollectionPolicyUpdateModelBuilder { Status = CollectionPolicyStatus.Draft, EnableLateFee = false }.Build()
			with { ReceiptGrouping = CollectionReceiptGrouping.Combined };
		var policy = new CollectionPolicy(Guid.NewGuid(), null, Guid.NewGuid(), model);
		var snapshot = CollectionPolicySnapshot.Deserialize(policy.ToSnapshot().Serialize());
		policy.Update(model with { ReceiptGrouping = CollectionReceiptGrouping.Separate });
		CollectionPolicy.FromSnapshot(policy.Id, null, policy.AcademicYearId, 1, snapshot).ReceiptGrouping.ShouldBe(CollectionReceiptGrouping.Combined);
		CollectionPolicySnapshot.Deserialize("{}").ReceiptGrouping.ShouldBe(CollectionReceiptGrouping.Separate);
		Should.Throw<BusinessException>(() => policy.Update(model with { ReceiptGrouping = (CollectionReceiptGrouping)99 }))
			.Code.ShouldBe(CollectionPolicyErrorCodes.InvalidReceiptGrouping);
	}

	private static FeeReceiptManager Manager()
	{
		var sequences = Substitute.For<IReceiptNumberSequenceRepository>();
		long sequence = 0;
		sequences.IncrementAndGetAsync(Arg.Any<int>(), Arg.Any<FeeReceiptKind?>(), Arg.Any<CancellationToken>())
			.Returns(_ => Interlocked.Increment(ref sequence));
		var clock = Substitute.For<IClock>();
		clock.Now.Returns(new DateTime(2026, 10, 23));
		return new FeeReceiptManager(sequences, clock, Substitute.For<ICurrentTenant>())
		{
			LazyServiceProvider = new AbpLazyServiceProvider(
				new ServiceCollection().AddSingleton<IGuidGenerator>(SimpleGuidGenerator.Instance).BuildServiceProvider())
		};
	}
}
