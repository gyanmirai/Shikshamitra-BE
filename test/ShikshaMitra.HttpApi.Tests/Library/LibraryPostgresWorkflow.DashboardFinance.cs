using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Migrated_Postgres_Dashboard_Http_Should_Show_Actual_Partial_Settlement_With_Only_Dashboard_Action()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.SourceFingerprintEvidence);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var fixture = await SeedFinancePgAsync(factory);
		var prepared = await PrepareFinancePgAsync(factory, database, fixture);
		using var maker = FinancePgClient(factory, FinancePgMakerId, FinancePgMakerRole);
		var form = await FinancePgGetAsync<FinanceExternalCollectionFormDto>(maker,
			FinancePgRoute + "/collect-form" + FinancePgSourceQuery(fixture.Source));
		form.CanCollect.ShouldBeTrue(string.Join(" ", form.SetupIssues));
		var collection = await PostAsync<FinanceExternalCollectionDto>(maker, FinancePgRoute + "/collections", new FinanceExternalCollectInput
		{
			Source = fixture.Source, Revision = form.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"),
			SchoolReceiptDate = new(2026, 11, 17), Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 40.25m }]
		});
		collection.Receivable.SettledPaymentAmount.ShouldBe(40.25m);
		collection.Receivable.OutstandingAmount.ShouldBe(60.50m);
		maker.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		maker.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.Library.Dashboard.View);
		using var response = await maker.GetAsync($"/api/library/dashboard/overview?academicYearId={factory.YearId:D}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		var overview = document.RootElement;
		overview.GetProperty("charges").GetProperty("pendingAmount").GetDecimal().ShouldBe(100.75m);
		var summary = overview.GetProperty("financePosition");
		summary.GetProperty("verifiedCount").GetInt32().ShouldBe(1);
		summary.GetProperty("needsReviewCount").GetInt32().ShouldBe(0);
		summary.GetProperty("unpostedCount").GetInt32().ShouldBe(0);
		var totals = summary.GetProperty("currencyTotals").EnumerateArray().ShouldHaveSingleItem();
		totals.GetProperty("currency").GetString().ShouldBe("INR");
		totals.GetProperty("originalAmountText").GetString().ShouldBe("100.75");
		totals.GetProperty("acceptedAmountText").GetString().ShouldBe("40.25");
		totals.GetProperty("settledAmountText").GetString().ShouldBe("40.25");
		totals.GetProperty("pendingVerificationAmountText").GetString().ShouldBe("0.00");
		totals.GetProperty("netWaivedAmountText").GetString().ShouldBe("0.00");
		totals.GetProperty("financialOutstandingAmountText").GetString().ShouldBe("60.50");
		totals.GetProperty("collectibleAmountText").GetString().ShouldBe("60.50");
		totals.GetProperty("refundedAmountText").GetString().ShouldBe("0.00");
		using var denied = await maker.GetAsync(FinancePgRoute + FinancePgSourceQuery(fixture.Source));
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await denied.Content.ReadAsStringAsync());
		(await FinancePgRowAsync(database, "Finance_ExternalReceivableSourceLinks", prepared.LinkId)).ShouldBe(prepared.OriginalLinkJson);
	}
}
