using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Approved_Bank_Balance_Presentation_Should_Distinguish_Canonical_Financial_Outstanding_From_Collectible_And_Return_Actual_Source_Year_Label()
	{
		var (context, collection) = await CollectTypedBankInstrumentAsync();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);
		var expected = await WithUnitOfWorkAsync(async () =>
		{
			var charge = await GetRequiredService<IRepository<Charge, Guid>>().GetAsync(context.Posted.FinanceChargeId);
			var ledger = await GetRequiredService<FinanceExternalChargeBalanceReader>().ReadAsync(charge);
			ledger.Amounts.Original.ShouldBe(100.75m);
			ledger.Amounts.Settled.ShouldBe(0m);
			ledger.Amounts.PendingVerification.ShouldBe(40.25m);
			ledger.Amounts.Unsettled.ShouldBe(100.75m);
			ledger.Amounts.Collectible.ShouldBe(60.50m);
			var year = await GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(context.Owner.YearId);
			year.TenantId.ShouldBe(context.Owner.TenantId);
			return (ledger.Amounts.Unsettled, year.Name);
		});
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		form.Receivable.OutstandingAmount.ShouldBe(60.50m);
		form.Receivable.PendingVerificationAmount.ShouldBe(40.25m);
		form.Receivable.AcademicYearId.ShouldBe(context.Owner.YearId);
		form.Receivable.Source.SourceVersion.ShouldBe(context.Post.Source.SourceVersion);
		var json = JsonSerializer.SerializeToElement(form.Receivable, new JsonSerializerOptions(JsonSerializerDefaults.Web));

		json.TryGetProperty("financialOutstandingAmount", out var financial).ShouldBeTrue(
			"Return the canonical unsettled balance; the browser must not derive this financial amount.");
		financial.GetDecimal().ShouldBe(expected.Unsettled);
		json.GetProperty("outstandingAmount").GetDecimal().ShouldBe(60.50m);
		json.TryGetProperty("academicYearName", out var yearName).ShouldBeTrue();
		yearName.GetString().ShouldBe(expected.Name);
		collection.Receivable.ApprovalStatus.ShouldBe(ChargeStatus.Approved);
	}
}
