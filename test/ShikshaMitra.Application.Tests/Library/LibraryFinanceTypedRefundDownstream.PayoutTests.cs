using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Library.Books;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Typed_Employee_Refund_Recorded_Payout_Should_Post_Once_With_The_Actual_Payer_And_Original_Payment_Intact()
	{
		var fixture = await SeedSubmittedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Administrator.TenantId);
		using var actor = SignIn(fixture.Administrator);
		await SeedApprovedRefundStateAsync(fixture);
		var input = RecordedTypedPayout(fixture);
		var service = DownstreamRefundService(fixture.Administrator);

		var processed = await WithUnitOfWorkAsync(() => service.RecordPayoutAsync(fixture.Submitted.Id, input));
		var replay = await WithUnitOfWorkAsync(() => service.RecordPayoutAsync(fixture.Submitted.Id, input));

		processed.Status.ShouldBe(FeeRefundStatus.Processed);
		processed.TotalAmount.ShouldBe(15.25m);
		processed.CalculatedAmount.ShouldBe(15m);
		processed.ApprovedAmount.ShouldBe(15m);
		processed.ProcessedAmount.ShouldBe(15m);
		processed.EmployeeId.ShouldBe(fixture.Source.Settlement.Owner.PersonId);
		replay.Id.ShouldBe(processed.Id);
		replay.Settlements.Single().Id.ShouldBe(processed.Settlements.Single().Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var request = await GetRequiredService<IFeeRefundRepository>().FindWithLinesAsync(processed.Id);
			request.ShouldNotBeNull();
			request.Settlements.Single().Status.ShouldBe(FeeRefundSettlementStatus.Processed);
			request.Settlements.Single().Amount.ShouldBe(15m);
			request.Settlements.Single().ExternalReference.ShouldBe(input.ExternalReference);
			request.Reservations.Single().Status.ShouldBe(FeeRefundReservationStatus.Consumed);
			request.Lines.Single().ProcessedAmount.ShouldBe(15m);
			var posting = (await GetRequiredService<IRepository<FeeRefundAccountingEntry, Guid>>()
				.GetListAsync(row => row.RefundRequestId == request.Id)).Single();
			posting.IsCorrection.ShouldBeFalse();
			posting.Amount.ShouldBe(15m);
			posting.SettlementId.ShouldBe(request.Settlements.Single().Id);
			posting.OriginalReceiptId.ShouldBe(fixture.Source.Collection.ReceiptId);
			var expense = (await GetRequiredService<IRepository<Transaction, Guid>>()
				.GetListAsync(row => row.SourceId == request.Id)).Single();
			expense.TransactionType.ShouldBe(TransactionType.Expense);
			expense.Amount.ShouldBe(15m);
			expense.EmployeeId.ShouldBe(fixture.Source.Settlement.Owner.PersonId);
			expense.StudentId.ShouldBeNull();
			expense.FinanceFeeAccountId.ShouldBeNull();
			expense.IsConfirmed.ShouldBeTrue();
		});
		var remaining = await WithUnitOfWorkAsync(() => TypedRefundService(fixture.Administrator).GetFormAsync(fixture.Source.Settlement.Post.Source));
		remaining.Receivable.RefundedAmount.ShouldBe(15m);
		remaining.Sources.Single().CommittedAmount.ShouldBe(15m);
		remaining.Sources.Single().AvailableAmount.ShouldBe(25.25m);
		await AssertOriginalTypedPaymentIntactAsync(fixture);
	}

	[Fact]
	public async Task Typed_Employee_Refund_Returned_Payout_Should_Append_An_Exact_Correction_And_Restore_Source_Eligibility()
	{
		var fixture = await SeedSubmittedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Administrator.TenantId);
		using var actor = SignIn(fixture.Administrator);
		await SeedApprovedRefundStateAsync(fixture);
		var originalPostingId = await SeedCompletedTypedRefundStateAsync(fixture);
		var settlementId = await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IFeeRefundRepository>().FindWithLinesAsync(fixture.Submitted.Id))!.Settlements.Single().Id);

		var returned = await WithUnitOfWorkAsync(() => DownstreamRefundService(fixture.Administrator).ReturnPayoutAsync(fixture.Submitted.Id, new()
		{
			SettlementId = settlementId, Reason = "Bank returned the evidenced UPI payout.",
			EvidenceReference = "Actual bank return reference LIB-RETURN-1"
		}));

		returned.Status.ShouldBe(FeeRefundStatus.Failed);
		returned.ProcessedAmount.ShouldBe(15m);
		returned.ReversedAmount.ShouldBe(15m);
		await WithUnitOfWorkAsync(async () =>
		{
			var entries = await GetRequiredService<IRepository<FeeRefundAccountingEntry, Guid>>()
				.GetListAsync(row => row.RefundRequestId == fixture.Submitted.Id);
			entries.Count.ShouldBe(2);
			entries.Single(row => !row.IsCorrection).Id.ShouldBe(originalPostingId);
			entries.Single(row => !row.IsCorrection).Amount.ShouldBe(15m);
			entries.Single(row => row.IsCorrection).CorrectsEntryId.ShouldBe(originalPostingId);
			entries.Single(row => row.IsCorrection).Amount.ShouldBe(15m);
			entries.Sum(row => row.IsCorrection ? -row.Amount : row.Amount).ShouldBe(0m);
			var expenses = await GetRequiredService<IRepository<Transaction, Guid>>().GetListAsync(row => row.SourceId == fixture.Submitted.Id);
			expenses.Count.ShouldBe(2);
			expenses.Sum(row => row.Amount).ShouldBe(0m);
			expenses.ShouldAllBe(row => row.EmployeeId == fixture.Source.Settlement.Owner.PersonId && !row.StudentId.HasValue && !row.FinanceFeeAccountId.HasValue);
		});
		var remaining = await WithUnitOfWorkAsync(() => TypedRefundService(fixture.Administrator).GetFormAsync(fixture.Source.Settlement.Post.Source));
		remaining.Receivable.RefundedAmount.ShouldBe(0m);
		remaining.Sources.Single().CommittedAmount.ShouldBe(0m);
		remaining.Sources.Single().AvailableAmount.ShouldBe(40.25m);
		await AssertOriginalTypedPaymentIntactAsync(fixture);
	}

	// Historical processed input isolates ReturnPayout from the separately tested
	// approval and RecordPayout paths; it uses their canonical named transitions.
	private Task<Guid> SeedCompletedTypedRefundStateAsync(DownstreamRefundFixture fixture) => WithUnitOfWorkAsync(async () =>
	{
		var repository = GetRequiredService<IFeeRefundRepository>();
		var request = await repository.FindWithLinesAsync(fixture.Submitted.Id)
			?? throw new InvalidOperationException("The approved typed request is missing.");
		var input = RecordedTypedPayout(fixture);
		request.SetPayoutRouting(fixture.ExpenseCategoryId, "Recorded Library refund", fixture.BankAccountId, "School Refund Bank", "UPI");
		var payout = request.RecordPayout(Guid.NewGuid(), fixture.Administrator.ActorId, input.IdempotencyKey,
			"UPI", 15m, input.PaymentDate, input.ExternalReference, null, fixture.BankAccountId, input.EvidenceReference!);
		await repository.StageNewSettlementAsync(payout);
		request.ConfirmPayout(payout.Id, fixture.Administrator.ActorId, input.IdempotencyKey, input.EvidenceReference!);
		var line = request.Lines.Single();
		var breakdown = JsonSerializer.Serialize(new[] { new { line.FeeHeadId, line.DueFeeId, line.ChargeId,
			line.LateFeeDueFeeId, line.SourceAllocationId, line.SourceReceiptId, Amount = line.ProcessedAmount } });
		var posting = new FeeRefundAccountingEntry(Guid.NewGuid(), fixture.Administrator.TenantId, request.Id,
			payout.Id, fixture.Source.Collection.ReceiptId, fixture.BankAccountId, "UPI", input.ExternalReference,
			15m, false, null, breakdown, input.EvidenceReference!, fixture.Administrator.ActorId);
		await GetRequiredService<IRepository<FeeRefundAccountingEntry, Guid>>().InsertAsync(posting, false);
		await GetRequiredService<AutomaticTransactionManager>().RecordAsync(fixture.Administrator.YearId,
			fixture.ExpenseCategoryId, "Recorded Library refund", fixture.BankAccountId, "School Refund Bank", request.VoucherNumber,
			15m, 6, TransactionType.Expense, $"refund:{payout.Id:N}", request.Id, null,
			paymentDate: input.PaymentDate.UtcDateTime, employeeId: fixture.Source.Settlement.Owner.PersonId);
		return posting.Id;
	});

	private Task AssertOriginalTypedPaymentIntactAsync(DownstreamRefundFixture fixture) => WithUnitOfWorkAsync(async () =>
	{
		(await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(fixture.Source.Settlement.Owner.AssessmentId)).Amount.ShouldBe(100.75m);
		(await GetRequiredService<IRepository<Charge, Guid>>().GetAsync(fixture.Source.Settlement.Posted.FinanceChargeId)).Amount.ShouldBe(100.75m);
		(await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>().GetAsync(fixture.Source.AllocationId)).Amount.ShouldBe(40.25m);
		(await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetAsync(fixture.Source.Collection.CollectionId)).TotalPaidFee.ShouldBe(40.25m);
		(await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().GetAsync(fixture.Source.Collection.ReceiptId)).TotalPaidAmount.ShouldBe(40.25m);
	});
}
