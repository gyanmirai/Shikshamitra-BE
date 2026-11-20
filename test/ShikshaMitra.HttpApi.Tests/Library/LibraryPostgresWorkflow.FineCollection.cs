using System;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Library.Reports;
using Shouldly;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Migrated_Postgres_Fine_Collection_Should_Read_Confirmed_School_Dates_And_Actual_Partial_Cash()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.SourceFingerprintEvidence);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var fixture = await SeedFinancePgAsync(factory, includeCurrentLibraryMember: true);
		var prepared = await PrepareFinancePgAsync(factory, database, fixture);
		using var maker = FinancePgClient(factory, FinancePgMakerId, FinancePgMakerRole);
		var form = await FinancePgGetAsync<FinanceExternalCollectionFormDto>(maker,
			FinancePgRoute + "/collect-form" + FinancePgSourceQuery(fixture.Source));
		form.CanCollect.ShouldBeTrue(string.Join(" ", form.SetupIssues));
		var collected = await PostAsync<FinanceExternalCollectionDto>(maker, FinancePgRoute + "/collections", new FinanceExternalCollectInput
		{
			Source = fixture.Source,
			Revision = form.Revision,
			IdempotencyKey = Guid.NewGuid().ToString("N"),
			SchoolReceiptDate = new(2026, 11, 17),
			Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 40.25m }]
		});
		collected.Receivable.SettledPaymentAmount.ShouldBe(40.25m);
		collected.Receivable.OutstandingAmount.ShouldBe(60.50m);

		SetMemberChargeTestAction(maker, ShikshaMitraSubmodulePermissions.Library.Reports.View);
		const string previewRoute = "/api/library/reports/preview";
		var request = new LibraryReportRequestDto
		{
			ReportType = LibraryReportTypes.FineCollection,
			Page = 1,
			PageSize = 1,
			Filters = new()
			{
				AcademicYearId = factory.YearId,
				MemberIds = [fixture.EmployeeId],
				MemberRole = 1,
				ChargeStatuses = [0],
				FromDate = new(2026, 11, 17),
				ToDate = new(2026, 11, 17)
			}
		};
		var report = await PostAsync<LibraryReportPreviewDto>(maker, previewRoute, request);
		report.TotalCount.ShouldBe(1);
		var row = report.Rows.ShouldHaveSingleItem();
		row["schoolChargeDate"].ShouldBe("2026-11-17");
		row["schoolDueDate"].ShouldBe("2026-11-24");
		row["amount"].ShouldBe("100.75");
		row["paymentStatus"].ShouldBe("Pending");
		row["financeEvidence"].ShouldBe("Verified");
		row["currency"].ShouldBe("INR");
		row["settledAmountText"].ShouldBe("40.25");
		row["collectibleAmountText"].ShouldBe("60.50");

		request.Filters.FromDate = new(2026, 11, 18);
		request.Filters.ToDate = new(2026, 11, 18);
		var excluded = await PostAsync<LibraryReportPreviewDto>(maker, previewRoute, request);
		excluded.TotalCount.ShouldBe(0);
		excluded.Rows.ShouldBeEmpty();

		request.ReportType = LibraryReportTypes.MemberList;
		request.Filters.FromDate = null;
		request.Filters.ToDate = null;
		request.Filters.ChargeStatuses = [];
		var memberList = await PostAsync<LibraryReportPreviewDto>(maker, previewRoute, request);
		memberList.TotalCount.ShouldBe(1);
		var member = memberList.Rows.ShouldHaveSingleItem();
		member["financeEvidence"].ShouldBe("Verified");
		member["currency"].ShouldBe("INR");
		member["collectibleAmountText"].ShouldBe("60.50");
		member["pendingCharges"].ShouldBe("100.75");
		memberList.Columns.ShouldContain(column => column.Id == "collectibleAmountText" && column.Label == "Verified collectible");
		memberList.Columns.ShouldNotContain(column => column.Id == "pendingCharges");
		(await FinancePgRowAsync(database, "Finance_ExternalReceivableSourceLinks", prepared.LinkId)).ShouldBe(prepared.OriginalLinkJson);
	}
}
