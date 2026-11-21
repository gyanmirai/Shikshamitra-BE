using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Finance.Accounts.CashManagement;
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

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	private static async Task AssertFinancePgCashReadAsync(LibraryPostgresApiTestFactory factory,
		FinancePgFixture fixture, FinanceExternalCollectionDto collected)
	{
		var cashId = await InFinancePgAsync(factory, async services =>
		{
			var row = await (await FinancePgDbAsync(services)).Set<CashCollection>().AsNoTracking()
				.SingleAsync(cash => cash.SourceId == collected.CollectionId);
			row.EmployeeId.ShouldBe(fixture.EmployeeId);
			row.StudentId.ShouldBeNull();
			row.FinanceFeeAccountId.ShouldBeNull();
			row.Amount.ShouldBe(40.25m);
			return row.Id;
		});
		using var maker = FinancePgClient(factory, FinancePgMakerId, FinancePgMakerRole);
		var root = "/api/finance/cash-management";
		var detail = await FinancePgGetAsync<CashDetail>(maker, $"{root}/{cashId:D}?academicYearId={factory.YearId:D}");
		detail.Id.ShouldBe(cashId);
		detail.SourceId.ShouldBe(collected.CollectionId);
		detail.StudentId.ShouldBeNull();
		detail.FinanceFeeAccountId.ShouldBeNull();
		detail.Amount.ShouldBe(40.25m);
		detail.RemainingAmount.ShouldBe(40.25m);
		var first = await FinancePgGetAsync<CashPage>(maker, $"{root}?academicYearId={factory.YearId:D}&page=1&pageSize=1");
		var second = await FinancePgGetAsync<CashPage>(maker, $"{root}?academicYearId={factory.YearId:D}&page=2&pageSize=1");
		first.TotalCount.ShouldBe(1);
		first.Items.Single().Id.ShouldBe(cashId);
		first.Items.Single().SourceId.ShouldBe(collected.CollectionId);
		first.Summary.Collected.ShouldBe(40.25m);
		first.YearSummary.Collected.ShouldBe(40.25m);
		first.AvailableCash.ShouldBe(40.25m);
		first.StatusCounts.All.ShouldBe(1);
		first.StatusCounts.Pending.ShouldBe(1);
		second.TotalCount.ShouldBe(1);
		second.Items.ShouldBeEmpty();
		second.Summary.Collected.ShouldBe(40.25m);
	}

	private static Task<Guid> AssertFinancePgOriginalPaymentAsync(LibraryPostgresApiTestFactory factory,
		FinancePgPrepared prepared, FinanceExternalCollectionDto collected) => InFinancePgAsync(factory, async services =>
	{
		var db = await FinancePgDbAsync(services);
		db.Database.ProviderName.ShouldBe("Npgsql.EntityFrameworkCore.PostgreSQL");
		var fixture = prepared.Fixture;
		var source = await db.Set<LibraryCharge>().AsNoTracking().SingleAsync(row => row.Id == fixture.Source.SourceId);
		source.Amount.ShouldBe(100.75m);
		source.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
		source.FinanceSourceLinkId.ShouldBe(prepared.LinkId);
		source.AcademicYearId.ShouldBe(factory.YearId);
		var charge = await db.Set<Charge>().AsNoTracking().SingleAsync(row => row.Id == prepared.Posted.FinanceChargeId);
		charge.Amount.ShouldBe(100.75m);
		charge.EmployeeId.ShouldBe(fixture.EmployeeId);
		charge.StudentId.ShouldBeNull();
		charge.AcademicYearId.ShouldBe(factory.YearId);
		charge.SchoolDueDate.ShouldBe(new DateOnly(2026, 11, 24));
		charge.DueDate.ShouldBeNull();
		var link = await db.Set<FinanceExternalReceivableSourceLink>().AsNoTracking().SingleAsync(row => row.Id == prepared.LinkId);
		link.CreationTime.Kind.ShouldBe(DateTimeKind.Utc);
		link.CreatorId.ShouldBe(FinancePgMakerId);
		link.CollectionPolicyVersionId.ShouldBeNull(); // Original Post stays unchanged; explicit binding is a separate immutable row.
		link.RefundPolicyVersionId.ShouldBeNull();
		var binding = await db.Set<FinanceExternalSettlementPolicy>().AsNoTracking().SingleAsync(row => row.SourceLinkId == link.Id);
		binding.CollectionPolicyVersionId.ShouldBe(fixture.CollectionPolicyVersionId);
		binding.RefundPolicyVersionId.ShouldBe(fixture.RefundPolicyVersionId);
		var header = await db.Set<FeeRecordMetadata>().AsNoTracking().SingleAsync(row => row.Id == collected.CollectionId);
		header.TotalPaidFee.ShouldBe(40.25m);
		header.EmployeeId.ShouldBe(fixture.EmployeeId);
		header.StudentId.ShouldBeNull();
		header.FinanceFeeAccountId.ShouldBeNull();
		header.Status.ShouldBe(FeeCollectionStatus.Paid);
		var allocations = await db.Set<ChargeCollectionRecord>().AsNoTracking()
			.Where(row => row.ChargeId == charge.Id).ToListAsync();
		allocations.Count.ShouldBe(1);
		var allocation = allocations.Single();
		allocation.Amount.ShouldBe(40.25m);
		allocation.FeeRecordMetadataId.ShouldBe(header.Id);
		allocation.EmployeeId.ShouldBe(fixture.EmployeeId);
		allocation.StudentId.ShouldBeNull();
		var instruments = await db.Set<PaymentMethodRecord>().AsNoTracking()
			.Where(row => row.FeeRecordMetadataId == header.Id).ToListAsync();
		instruments.Count.ShouldBe(1);
		instruments.Single().EmployeeId.ShouldBe(fixture.EmployeeId);
		instruments.Single().StudentId.ShouldBeNull();
		instruments.Single().FinanceFeeAccountId.ShouldBeNull();
		instruments.Single().Amount.ShouldBe(40.25m);
		var receipts = await db.Set<FeePaymentReceipt>().AsNoTracking()
			.Where(row => row.FeeCollectionTransactionId == header.Id).ToListAsync();
		receipts.Count.ShouldBe(1);
		var receipt = receipts.Single();
		receipt.Id.ShouldBe(collected.ReceiptId);
		receipt.TotalPaidAmount.ShouldBe(40.25m);
		receipt.ReceiptDate.ShouldBe(new DateOnly(2026, 11, 17));
		receipt.ReceiptNumber.ShouldBe(collected.ReceiptNumber);
		receipt.EmployeeId.ShouldBe(fixture.EmployeeId);
		receipt.StudentId.ShouldBeNull();
		receipt.FinanceFeeAccountId.ShouldBeNull();
		receipt.ReceiptKind.ShouldBe(FeeReceiptKind.FinalCharge);
		var sequence = await db.Set<ReceiptNumberSequence>().AsNoTracking().SingleAsync(row =>
			row.StoredTenantId == FinancePgTenantId && row.Year == 2026 && row.StoredKind == (int)FeeReceiptKind.FinalCharge);
		sequence.NextValue.ShouldBe(2L, "One actual final-charge receipt was issued; replays and reads must not consume another number.");
		return allocation.Id;
	});

	private static Task<int> AssertFinancePgRefundStageAsync(LibraryPostgresApiTestFactory factory,
		FinancePgPrepared prepared, Guid requestId, FeeRefundStatus expectedStatus,
		FeeRefundReservationStatus? expectedReservation, decimal processed, decimal reversed, int entryCount, int expenseCount) =>
		InFinancePgAsync(factory, async services =>
		{
			var db = await FinancePgDbAsync(services);
			var request = await services.GetRequiredService<IFeeRefundRepository>().FindWithLinesAsync(requestId);
			request.ShouldNotBeNull();
			request.Status.ShouldBe(expectedStatus);
			request.AcademicYearId.ShouldBe(factory.YearId);
			request.EmployeeId.ShouldBe(prepared.Fixture.EmployeeId);
			request.StudentId.ShouldBeNull();
			request.StudentFeeStructureId.ShouldBeNull();
			request.FinanceFeeAccountId.ShouldBeNull();
			request.FinanceExternalSourceLinkId.ShouldBe(prepared.LinkId);
			request.RefundPolicyVersionId.ShouldBe(prepared.Fixture.RefundPolicyVersionId);
			request.TotalAmount.ShouldBe(15.25m);
			request.CalculatedAmount.ShouldBe(15m);
			request.ProcessedAmount.ShouldBe(processed);
			request.ReversedAmount.ShouldBe(reversed);
			request.Lines.Single().ProcessedAmount.ShouldBe(processed);
			request.Lines.Single().ReversedAmount.ShouldBe(reversed);
			if (expectedReservation.HasValue)
			{
				request.Reservations.Count.ShouldBe(1);
				request.Reservations.Single().Status.ShouldBe(expectedReservation.Value);
				request.Reservations.Single().Amount.ShouldBe(15m);
			}
			else request.Reservations.ShouldBeEmpty();
			var postings = await db.Set<FeeRefundAccountingEntry>().AsNoTracking()
				.Where(row => row.RefundRequestId == requestId).ToListAsync();
			postings.Count.ShouldBe(entryCount);
			postings.Sum(row => row.IsCorrection ? -row.Amount : row.Amount).ShouldBe(processed - reversed);
			postings.ShouldAllBe(row => row.Amount == 15m && row.OriginalReceiptId == request.OriginalReceiptId);
			if (entryCount == 2)
			{
				var original = postings.Single(row => !row.IsCorrection);
				postings.Single(row => row.IsCorrection).CorrectsEntryId.ShouldBe(original.Id);
			}
			var expenses = await db.Set<Transaction>().AsNoTracking().Where(row => row.SourceId == requestId).ToListAsync();
			expenses.Count.ShouldBe(expenseCount);
			expenses.Sum(row => row.Amount).ShouldBe(processed - reversed);
			expenses.ShouldAllBe(row => row.TransactionType == TransactionType.Expense &&
				row.EmployeeId == prepared.Fixture.EmployeeId && !row.StudentId.HasValue &&
				!row.FinanceFeeAccountId.HasValue && row.IsConfirmed);
			// No money is posted while only Submitted/Approved; approval creates a reservation.
			if (expectedStatus is FeeRefundStatus.Submitted or FeeRefundStatus.Approved)
				request.Settlements.ShouldBeEmpty();
			else request.Settlements.Count.ShouldBe(1);
			return 0;
		});

	private static async Task AssertFinancePgRefundableAsync(LibraryPostgresApiTestFactory factory,
		FinancePgFixture fixture, decimal committed, decimal available, decimal refunded)
	{
		using var maker = FinancePgClient(factory, FinancePgMakerId, FinancePgMakerRole);
		var form = await FinancePgGetAsync<FinanceExternalRefundFormDto>(maker,
			FinancePgRoute + "/refund-form" + FinancePgSourceQuery(fixture.Source));
		form.Sources.Single().PaidAmount.ShouldBe(40.25m);
		form.Sources.Single().CommittedAmount.ShouldBe(committed);
		form.Sources.Single().AvailableAmount.ShouldBe(available);
		form.Receivable.RefundedAmount.ShouldBe(refunded);
		form.Receivable.SettledPaymentAmount.ShouldBe(40.25m);
		form.Receivable.OutstandingAmount.ShouldBe(60.50m);
		(form.Sources.Single().CommittedAmount + form.Sources.Single().AvailableAmount).ShouldBe(40.25m);
	}
}

