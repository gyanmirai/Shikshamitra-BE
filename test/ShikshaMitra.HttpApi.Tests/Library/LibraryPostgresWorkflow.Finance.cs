using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShikshaMitra.Finance.Accounts.Reconciliation;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Migrated_Postgres_Employee_Finance_Should_Post_Collect_Refund_Pay_Return_And_Preserve_Immutable_Evidence()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.SourceFingerprintEvidence);
		(await database.ScalarAsync("SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\""))
			.ShouldBe(LibraryMigrationPostgresFixture.SourceFingerprintEvidence);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		using var maker = FinancePgClient(factory, FinancePgMakerId, FinancePgMakerRole);
		var fixture = await SeedFinancePgAsync(factory);
		var originalStudentCount = await InFinancePgAsync(factory, async services =>
			await (await FinancePgDbAsync(services)).Set<Student>().CountAsync());
		var originalAssignmentCount = await InFinancePgAsync(factory, async services =>
			await (await FinancePgDbAsync(services)).Set<StudentFeeStructure>().CountAsync());
		var prepared = await PrepareFinancePgAsync(factory, database, fixture);
		using var reviewer = FinancePgClient(factory, fixture.ReviewerId, fixture.ReviewerRole);
		var form = await FinancePgGetAsync<FinanceExternalCollectionFormDto>(maker,
			FinancePgRoute + "/collect-form" + FinancePgSourceQuery(fixture.Source));
		form.CanCollect.ShouldBeTrue(string.Join(" ", form.SetupIssues));
		var collectionInput = new FinanceExternalCollectInput
		{
			Source = fixture.Source, Revision = form.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"),
			SchoolReceiptDate = new(2026, 11, 17),
			Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 40.25m }]
		};
		var collected = await PostAsync<FinanceExternalCollectionDto>(maker, FinancePgRoute + "/collections", collectionInput);
		var collectionReplay = await PostAsync<FinanceExternalCollectionDto>(maker, FinancePgRoute + "/collections", collectionInput);
		collectionReplay.CollectionId.ShouldBe(collected.CollectionId);
		collectionReplay.ReceiptId.ShouldBe(collected.ReceiptId);
		collectionReplay.ReceiptNumber.ShouldBe(collected.ReceiptNumber);
		collected.AcceptedAmount.ShouldBe(40.25m);
		collected.Receivable.SettledPaymentAmount.ShouldBe(40.25m);
		collected.Receivable.PendingVerificationAmount.ShouldBe(0m);
		collected.Receivable.OutstandingAmount.ShouldBe(60.50m);
		var allocationId = await AssertFinancePgOriginalPaymentAsync(factory, prepared, collected);
		await AssertFinancePgConsumerAsync(factory, prepared, settled: 40.25m, pending: 0m, refunded: 0m);
		await AssertFinancePgCashReadAsync(factory, fixture, collected);
		// Real receipt generation through the current authenticated endpoint, not a blob stub.
		using (var pdf = await maker.GetAsync($"/api/v1/fee-receipts/{collected.ReceiptId:D}/pdf"))
		{
			pdf.StatusCode.ShouldBe(HttpStatusCode.OK, await pdf.Content.ReadAsStringAsync());
			pdf.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
			var text = ReceiptPdfTextReader.Read(await pdf.Content.ReadAsByteArrayAsync());
			text.ShouldContain("Maya José");
			text.ShouldContain(collected.ReceiptNumber);
		}
		var refundForm = await FinancePgGetAsync<FinanceExternalRefundFormDto>(maker,
			FinancePgRoute + "/refund-form" + FinancePgSourceQuery(fixture.Source));
		refundForm.CanRequest.ShouldBeTrue(string.Join(" ", refundForm.SetupIssues));
		refundForm.Sources.Single().AvailableAmount.ShouldBe(40.25m);
		var refundInput = new FinanceExternalRefundRequestInput
		{
			Source = fixture.Source, Revision = refundForm.Revision, OriginalReceiptId = collected.ReceiptId,
			Method = "UPI", IdempotencyKey = Guid.NewGuid().ToString("N"),
			Reason = "Requested a reviewed partial refund of the actual settled staff recovery payment.",
			Lines = [new() { SourceAllocationId = allocationId, SourceReceiptId = collected.ReceiptId, Amount = 15.25m }]
		};
		var preview = await PostAsync<FeeRefundPreviewDto>(maker, FinancePgRoute + "/refunds/preview", refundInput);
		preview.RequestedAmount.ShouldBe(15.25m);
		preview.SettledPaidAmount.ShouldBe(40.25m);
		preview.MaximumAllowedAmount.ShouldBe(15m);
		preview.IsAllowed.ShouldBeTrue();
		var submitted = await PostAsync<FeeRefundRequestDto>(maker, FinancePgRoute + "/refunds", refundInput);
		var refundReplay = await PostAsync<FeeRefundRequestDto>(maker, FinancePgRoute + "/refunds", refundInput);
		refundReplay.Id.ShouldBe(submitted.Id);
		submitted.TotalAmount.ShouldBe(15.25m);
		submitted.CalculatedAmount.ShouldBe(15m);
		submitted.RefundPolicyVersionId.ShouldBe(fixture.RefundPolicyVersionId);
		await AssertFinancePgRefundSourceListAsync(maker, factory.YearId, prepared.LinkId, submitted.Id);
		await AssertFinancePgRefundStageAsync(factory, prepared, submitted.Id, FeeRefundStatus.Submitted,
			null, 0m, 0m, 0, 0);
		await AssertFinancePgRefundableAsync(factory, fixture, committed: 15m, available: 25.25m, refunded: 0m);
		var approved = await PostAsync<FeeRefundRequestDto>(reviewer, $"/api/fee-refunds/{submitted.Id:D}/approve",
			new FeeRefundActionDto { Method = "UPI", Notes = "Independent review of the original settled source and pinned rules." });
		approved.ApprovedAmount.ShouldBe(15m);
		await AssertFinancePgRefundStageAsync(factory, prepared, submitted.Id, FeeRefundStatus.Approved,
			FeeRefundReservationStatus.Active, 0m, 0m, 0, 0);
		var requestedPayoutInstant = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromMinutes(330));
		var payoutInput = new RecordRefundPayoutDto
		{
			IdempotencyKey = Guid.NewGuid().ToString("N"), Method = "UPI", Amount = 15m,
			PaymentDate = requestedPayoutInstant,
			ExpenseCategoryId = fixture.ExpenseCategoryId, BankAccountId = fixture.BankId,
			ExternalReference = "UPI-" + Guid.NewGuid().ToString("N"),
			EvidenceReference = "School bank statement confirms this actual completed payout."
		};
		var processed = await PostAsync<FeeRefundRequestDto>(reviewer, $"/api/fee-refunds/{submitted.Id:D}/payout", payoutInput);
		processed.Settlements.Single().SettlementDate.Offset.ShouldBe(TimeSpan.Zero);
		(processed.Settlements.Single().SettlementDate - requestedPayoutInstant).Duration()
			.ShouldBeLessThan(TimeSpan.FromMilliseconds(1));
		payoutInput.PaymentDate = requestedPayoutInstant.ToUniversalTime();
		var payoutReplay = await PostAsync<FeeRefundRequestDto>(reviewer, $"/api/fee-refunds/{submitted.Id:D}/payout", payoutInput);
		payoutReplay.Settlements.Single().Id.ShouldBe(processed.Settlements.Single().Id);
		processed.ProcessedAmount.ShouldBe(15m);
		await AssertFinancePgRefundStageAsync(factory, prepared, submitted.Id, FeeRefundStatus.Processed,
			FeeRefundReservationStatus.Consumed, 15m, 0m, 1, 1);
		await AssertFinancePgRefundableAsync(factory, fixture, committed: 15m, available: 25.25m, refunded: 15m);
		await AssertFinancePgConsumerAsync(factory, prepared, settled: 40.25m, pending: 0m, refunded: 15m);
		var accountingId = await InFinancePgAsync(factory, async services =>
			(await services.GetRequiredService<IRepository<FeeRefundAccountingEntry, Guid>>()
				.GetListAsync(row => row.RefundRequestId == submitted.Id)).Single().Id);
		var originalAccounting = await FinancePgRowAsync(database, "Finance_FeeRefundAccountingEntries", accountingId);
		using (var pdf = await reviewer.GetAsync($"/api/fee-refunds/{submitted.Id:D}/receipt/pdf"))
		{
			pdf.StatusCode.ShouldBe(HttpStatusCode.OK, await pdf.Content.ReadAsStringAsync());
			pdf.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
			var text = ReceiptPdfTextReader.Read(await pdf.Content.ReadAsByteArrayAsync());
			text.ShouldContain(submitted.VoucherNumber);
			text.ShouldContain(collected.ReceiptNumber);
		}
		var returned = await PostAsync<FeeRefundRequestDto>(reviewer, $"/api/fee-refunds/{submitted.Id:D}/payout/return",
			new ReturnRefundPayoutDto
			{
				SettlementId = processed.Settlements.Single().Id, Reason = "Bank returned the evidenced UPI payout.",
				EvidenceReference = "Actual bank return reference PG-LIBRARY-RETURN-1"
			});
		returned.Status.ShouldBe(FeeRefundStatus.Failed);
		returned.ProcessedAmount.ShouldBe(15m);
		returned.ReversedAmount.ShouldBe(15m);
		await AssertFinancePgRefundStageAsync(factory, prepared, submitted.Id, FeeRefundStatus.Failed,
			FeeRefundReservationStatus.Consumed, 15m, 15m, 2, 2);
		await AssertFinancePgRefundableAsync(factory, fixture, committed: 0m, available: 40.25m, refunded: 0m);
		await AssertFinancePgConsumerAsync(factory, prepared, settled: 40.25m, pending: 0m, refunded: 0m);
		(await FinancePgRowAsync(database, "Finance_FeeRefundAccountingEntries", accountingId)).ShouldBe(originalAccounting);
		await AssertFinancePgOriginalPaymentAsync(factory, prepared, collected);
		(await FinancePgRowAsync(database, "Finance_ExternalReceivableSourceLinks", prepared.LinkId)).ShouldBe(prepared.OriginalLinkJson);
		(await database.ScalarAsync($"""
			SELECT to_jsonb(t)::text FROM "Finance_ExternalReceivableCommands" t
			WHERE "SourceLinkId"='{prepared.LinkId:D}' AND "Operation"='Post';
			""")).ShouldBe(prepared.OriginalPostJson);
		await InFinancePgAsync(factory, async services =>
		{
			var db = await FinancePgDbAsync(services);
			(await db.Set<Student>().CountAsync()).ShouldBe(originalStudentCount);
			(await db.Set<StudentFeeStructure>().CountAsync()).ShouldBe(originalAssignmentCount);
			var commands = await db.Set<FinanceExternalReceivableCommand>().AsNoTracking()
				.Where(row => row.SourceLinkId == prepared.LinkId).ToListAsync();
			commands.Count.ShouldBe(4); // Post, BindPolicy, Approve, Collect; replays create no commands.
			commands.Select(row => row.Operation).OrderBy(value => value).ShouldBe(new[] { "Approve", "BindPolicy", "Collect", "Post" });
			commands.ShouldAllBe(row => row.ActorId == FinancePgMakerId || row.ActorId == fixture.ReviewerId);
			(await db.Set<FeeRefundRequest>().CountAsync(row => row.FinanceExternalSourceLinkId == prepared.LinkId)).ShouldBe(1);
			return 0;
		});
	}

	[LibraryPostgresFact]
	public async Task Migrated_Postgres_Employee_Collection_With_Default_NonUtc_Clock_Should_Save_Utc_Instants_And_Explicit_School_Date()
	{
		// This acceptance case intentionally requires the worker's actual India local timezone.
		// It never changes the process timezone or the host/audit clock.
		var defaultClock = new Clock(Options.Create(new AbpClockOptions()));
		defaultClock.Now.Kind.ShouldNotBe(DateTimeKind.Utc);
		new DateTimeOffset(defaultClock.Now).Offset.ShouldBe(TimeSpan.FromMinutes(330),
			"This gate proves the actual non-UTC (+05:30) worker boundary rather than repeating the UTC-host control.");
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.CataloguePrice);
		await using var factory = new LibraryPostgresApiTestFactory(database, collectionClock: defaultClock);
		using var maker = FinancePgClient(factory, FinancePgMakerId, FinancePgMakerRole);
		var fixture = await SeedFinancePgAsync(factory);
		var prepared = await PrepareFinancePgAsync(factory, database, fixture);
		var form = await FinancePgGetAsync<FinanceExternalCollectionFormDto>(maker,
			FinancePgRoute + "/collect-form" + FinancePgSourceQuery(fixture.Source));
		var input = new FinanceExternalCollectInput
		{
			Source = fixture.Source, Revision = form.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"),
			SchoolReceiptDate = new(2026, 11, 17),
			Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 40.25m }]
		};
		var before = DateTimeOffset.UtcNow;
		var collected = await PostAsync<FinanceExternalCollectionDto>(maker, FinancePgRoute + "/collections", input);
		var after = DateTimeOffset.UtcNow;
		collected.SchoolReceiptDate.ShouldBe(new DateOnly(2026, 11, 17));
		await AssertFinancePgOriginalPaymentAsync(factory, prepared, collected);
		await InFinancePgAsync(factory, async services =>
		{
			services.GetRequiredService<IClock>().Now.Kind.ShouldBe(DateTimeKind.Utc);
			var db = await FinancePgDbAsync(services);
			var allocation = await db.Set<ChargeCollectionRecord>().AsNoTracking()
				.SingleAsync(row => row.FeeRecordMetadataId == collected.CollectionId);
			var payment = await db.Set<PaymentMethodRecord>().AsNoTracking()
				.SingleAsync(row => row.FeeRecordMetadataId == collected.CollectionId);
			foreach (var instant in new[] { allocation.PaymentDate, payment.PaymentDate })
			{
				instant.Offset.ShouldBe(TimeSpan.Zero);
				instant.ShouldBeGreaterThanOrEqualTo(before.AddMilliseconds(-1));
				instant.ShouldBeLessThanOrEqualTo(after.AddMilliseconds(1));
			}
			// Framework-generated audit fields continue to use the untouched host UTC clock.
			allocation.CreationTime.Kind.ShouldBe(DateTimeKind.Utc);
			payment.CreationTime.Kind.ShouldBe(DateTimeKind.Utc);
			allocation.CreatorId.ShouldBe(FinancePgMakerId);
			payment.CreatorId.ShouldBe(FinancePgMakerId);
			(await db.Set<FeePaymentReceipt>().AsNoTracking().SingleAsync(row => row.Id == collected.ReceiptId))
				.ReceiptDate.ShouldBe(new DateOnly(2026, 11, 17));
			return 0;
		});
	}
}

