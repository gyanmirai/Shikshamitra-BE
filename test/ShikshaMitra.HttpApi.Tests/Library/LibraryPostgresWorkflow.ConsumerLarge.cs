using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using Shouldly;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Migrated_Postgres_Canonical_Consumer_Should_Aggregate_500_Then_2500_Valid_Sources_Within_Default_Timeout()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.SourceFingerprintEvidence);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var fixture = await SeedFinancePgAsync(factory);
		var anchor = await PrepareFinancePgAsync(factory, database, fixture);
		await ConfigureFinancePgDashboardScaleClockAsync(factory);
		using var maker = FinancePgClient(factory, FinancePgMakerId, FinancePgMakerRole);
		var previous = 1; // This first source completed actual Post/Bind/independent approval HTTP.
		FinanceConsumerLargeMeasurement? first = null;
		FinanceDashboardLargeMeasurement? firstDashboard = null;
		foreach (var population in new[] { 500, 2500 })
		{
			var samples = await SeedFinancePgConsumerLargeAsync(factory, fixture, population - previous);
			samples.AddedCount.ShouldBe(population - previous);
			foreach (var sample in new[] { samples.First, samples.Last })
			{
				var read = await FinancePgGetAsync<FinanceExternalReceivableDto>(maker,
					FinancePgRoute + FinancePgSourceQuery(sample.Source));
				read.FinanceChargeId.ShouldBe(sample.FinanceChargeId);
				read.Payer.EmployeeId.ShouldBe(fixture.EmployeeId); read.Payer.StudentId.ShouldBeNull();
				read.AcademicYearId.ShouldBe(factory.YearId); read.Currency.ShouldBe("INR");
				read.ApprovalStatus.ShouldBe(ChargeStatus.Approved);
				read.OriginalAmount.ShouldBe(100.75m); read.OutstandingAmount.ShouldBe(100.75m);
				read.AcceptedPaymentAmount.ShouldBe(0m); read.SettledPaymentAmount.ShouldBe(0m);
				var replay = await PostAsync<FinanceExternalReceivableDto>(maker, FinancePgRoute,
					new PostFinanceExternalReceivableInput { Source = sample.Source, FinanceChargeCategoryId = fixture.ChargeCategoryId,
						IdempotencyKey = sample.PostKey });
				replay.FinanceChargeId.ShouldBe(sample.FinanceChargeId);
			}
			var measured = await MeasureFinancePgConsumerLargeAsync(factory, population);
			var dashboard = await MeasureFinancePgPublicDashboardAsync(factory, population, anchor.OriginalLinkJson);
			if (firstDashboard == null) firstDashboard = dashboard;
			else dashboard.SqlLengths.ShouldBe(firstDashboard.SqlLengths, "Dashboard SQL must not grow with the source population.");
			if (first == null) first = measured;
			else
			{
				measured.Commands.ShouldBe(first.Commands);
				measured.SqlLengths.ShouldBe(first.SqlLengths,
					"The same provider query must not expand into one expression or parameter per source.");
			}
			previous = population;
		}
		(await FinancePgRowAsync(database, "Finance_ExternalReceivableSourceLinks", anchor.LinkId)).ShouldBe(anchor.OriginalLinkJson);
	}
}
