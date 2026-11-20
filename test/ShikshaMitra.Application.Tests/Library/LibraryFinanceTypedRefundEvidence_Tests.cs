using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Typed_Refund_Preview_And_Create_Should_Use_The_Same_Accepted_Upi_Method_With_Explicit_Details()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Settlement.Owner.TenantId);
		using var actor = SignIn(fixture.Settlement.Owner);
		var service = TypedRefundService(fixture.Settlement.Owner);
		var form = await WithUnitOfWorkAsync(() => service.GetFormAsync(fixture.Settlement.Post.Source));
		var input = RefundRequest(fixture, form.Revision, 15.25m);
		input.Method = "Upi";
		input.PaymentDetails = ExplicitRefundDetails();

		var preview = await WithUnitOfWorkAsync(() => service.PreviewAsync(input));
		var submitted = await WithUnitOfWorkAsync(() => service.CreateAsync(input));

		preview.MaximumAllowedAmount.ShouldBe(15m);
		submitted.Status.ShouldBe(FeeRefundStatus.Submitted);
		submitted.TotalAmount.ShouldBe(15.25m);
		submitted.CalculatedAmount.ShouldBe(15m);
		submitted.RefundMethod.ShouldBe("UPI");
		submitted.ProcessedAmount.ShouldBe(0m);
	}

	[Fact]
	public async Task Typed_Refund_Replay_Should_Not_Depend_On_Payment_Detail_Property_Order()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Settlement.Owner.TenantId);
		using var actor = SignIn(fixture.Settlement.Owner);
		var service = TypedRefundService(fixture.Settlement.Owner);
		var form = await WithUnitOfWorkAsync(() => service.GetFormAsync(fixture.Settlement.Post.Source));
		var input = RefundRequest(fixture, form.Revision, 15.25m);
		input.PaymentDetails = ExplicitRefundDetails();
		var submitted = await WithUnitOfWorkAsync(() => service.CreateAsync(input));
		input.PaymentDetails = input.PaymentDetails.Reverse().ToDictionary(pair => pair.Key, pair => pair.Value);

		var replay = await WithUnitOfWorkAsync(() => service.CreateAsync(input));

		replay.Id.ShouldBe(submitted.Id);
		replay.TotalAmount.ShouldBe(15.25m);
		replay.CalculatedAmount.ShouldBe(15m);
		await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<FeeRefundRequest, Guid>>().CountAsync()).ShouldBe(1));
	}

	[Fact]
	public async Task Typed_Refund_Should_Reject_A_Contradictory_Receipt_Amount_Before_Exposing_Refundable_Money()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Settlement.Owner.TenantId);
		using var actor = SignIn(fixture.Settlement.Owner);
		await WithUnitOfWorkAsync(async () =>
		{
			var receipts = GetRequiredService<IRepository<FeePaymentReceipt, Guid>>();
			var receipt = await receipts.GetAsync(fixture.Collection.ReceiptId);
			receipt.TotalPaidAmount.ShouldBe(40.25m);
			receipt.TotalPaidAmount = 1m;
			await receipts.UpdateAsync(receipt, true);
		});

		var error = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			TypedRefundService(fixture.Settlement.Owner).GetFormAsync(fixture.Settlement.Post.Source)));

		error.Code.ShouldBe(FeeRefundErrorCodes.SourceReceiptInvalid);
		await AssertNoTypedRefundsAsync();
	}

	[Fact]
	public async Task Distinct_Refund_Keys_Should_Count_Submitted_Source_Claims_Without_Creating_Funding_Reservations()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Settlement.Owner.TenantId);
		using var actor = SignIn(fixture.Settlement.Owner);
		var service = TypedRefundService(fixture.Settlement.Owner);
		var before = await WithUnitOfWorkAsync(() => service.GetFormAsync(fixture.Settlement.Post.Source));
		var submitted = await WithUnitOfWorkAsync(() => service.CreateAsync(RefundRequest(fixture, before.Revision, 15.25m)));
		var current = await WithUnitOfWorkAsync(() => service.GetFormAsync(fixture.Settlement.Post.Source));
		current.Sources.Single().CommittedAmount.ShouldBe(15m);
		current.Sources.Single().AvailableAmount.ShouldBe(25.25m);
		submitted.ProcessedAmount.ShouldBe(0m);
		var stale = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			service.CreateAsync(RefundRequest(fixture, before.Revision, 30m))));
		stale.Code.ShouldBe(LibraryFinanceErrorCodes.InvalidBalanceRevision);

		var overlapping = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			service.CreateAsync(RefundRequest(fixture, current.Revision, 30m))));

		overlapping.Code.ShouldBe(FeeRefundErrorCodes.InsufficientRefundBalance);
		await WithUnitOfWorkAsync(async () =>
		{
			var requests = await GetRequiredService<ShikshaMitra.Finance.Fees.FeeRefund.Repositories.IFeeRefundRepository>().FindWithLinesAsync(submitted.Id);
			requests.ShouldNotBeNull();
			requests.Reservations.ShouldBeEmpty();
			requests.Settlements.ShouldBeEmpty();
			(await GetRequiredService<IRepository<FeeRefundRequest, Guid>>().CountAsync()).ShouldBe(1);
		});
	}

	private static Dictionary<string, object?> ExplicitRefundDetails() => new()
	{
		["refundDate"] = "2026-11-18", ["sourceBankAccount"] = "Proposed school payout account",
		["beneficiaryName"] = "Library Borrower", ["upiId"] = "borrower@example"
	};
}
