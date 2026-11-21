using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Library.Charges;
using Shouldly;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Migrated_Postgres_Canonical_Consumer_Should_Keep_Reserved_Bank_Money_And_Review_Before_Aggregates()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.SourceFingerprintEvidence);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		using var maker = FinancePgClient(factory, FinancePgMakerId, FinancePgMakerRole);
		var fixture = await SeedFinancePgAsync(factory);
		var prepared = await PrepareFinancePgAsync(factory, database, fixture);
		var form = await FinancePgGetAsync<FinanceExternalCollectionFormDto>(maker,
			FinancePgRoute + "/collect-form" + FinancePgSourceQuery(fixture.Source));
		form.CanCollect.ShouldBeTrue(string.Join(" ", form.SetupIssues));
		form.PaymentMethods.ShouldContain(PaymentMethodEnum.UPI);
		var input = new FinanceExternalCollectInput
		{
			Source = fixture.Source, Revision = form.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"),
			SchoolReceiptDate = new(2026, 11, 17), Payments = [new()
			{
				PaymentMethod = PaymentMethodEnum.UPI, Amount = 100.75m, BankAccountId = fixture.BankId,
				Details = new() { ["upiTransactionId"] = "NATIVE-CONSUMER-RESERVED", ["upiId"] = "school@bank" }
			}]
		};
		var collected = await PostAsync<FinanceExternalCollectionDto>(maker, FinancePgRoute + "/collections", input);
		collected.Receivable.AcceptedPaymentAmount.ShouldBe(100.75m);
		collected.Receivable.SettledPaymentAmount.ShouldBe(0m);
		collected.Receivable.PendingVerificationAmount.ShouldBe(100.75m);
		await AssertFinancePgConsumerAsync(factory, prepared, settled: 0m, pending: 100.75m, refunded: 0m);
		var one = await MeasureFinancePgConsumerScaleAsync(factory, expectedCount: 1, expectedReview: 0, expectedOutstanding: 100.75m);
		await SeedFinancePgConsumerSiblingsAsync(factory, fixture, count: 7);
		var eight = await MeasureFinancePgConsumerScaleAsync(factory, expectedCount: 8, expectedReview: 0, expectedOutstanding: 100.75m);
		one.Commands.ShouldBe(eight.Commands);
		// The same parameterized query shape must not grow with seven more rows.
		one.SqlLengths.ShouldBe(eight.SqlLengths);
		await InFinancePgAsync(factory, async services =>
		{
			var db = await FinancePgDbAsync(services);
			var header = await db.Set<FeeRecordMetadata>().SingleAsync(row => row.Id == collected.CollectionId);
			header.TotalPaidFee.ShouldBe(100.75m);
			header.Status.ShouldBe(FeeCollectionStatus.InReconciliation);
			// A real soft-deleted header remains physically present. Invalid siblings
			// must be classified before aggregate/count/page, without deleting allocations.
			header.IsDeleted = true;
			await db.SaveChangesAsync();
			return 0;
		});
		await MeasureFinancePgConsumerScaleAsync(factory, expectedCount: 8, expectedReview: 1, expectedOutstanding: 0m);
		await InFinancePgAsync(factory, async services =>
		{
			using var principal = ConsumerPgPrincipal(services);
			var reader = await ConsumerPgReaderAsync(services);
			var detail = (await reader.ReadAsync(new(LibraryChargePositionReadPurpose.ChargeHistory,
				factory.YearId, [fixture.Source.SourceId]))).ShouldHaveSingleItem();
			detail.Evidence.ShouldBe(FinanceExternalPositionEvidence.NeedsReview);
			detail.Amounts.ShouldBeNull(); detail.ReviewIssues.ShouldNotBeEmpty();
			return 0;
		});
		(await FinancePgRowAsync(database, "Finance_ExternalReceivableSourceLinks", prepared.LinkId)).ShouldBe(prepared.OriginalLinkJson);
	}
}
