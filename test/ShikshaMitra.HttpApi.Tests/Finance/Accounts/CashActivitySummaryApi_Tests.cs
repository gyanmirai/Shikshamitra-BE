using System;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Finance.Accounts.CashManagement;
using ShikshaMitra.Finance.FeeReceiptTests;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

[Collection(FinanceReceiptApiTestCollection.Name)]
public class CashActivitySummaryApi_Tests(FinanceReceiptApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Activity_Uses_Each_Event_Month_And_Leaves_Source_Rows_And_Year_Cash_Intact(
		bool legacyWithoutCreditEvents, bool unknownLegacyCreditDate)
	{
		var reference = $"MONTHLY-{Guid.NewGuid():N}";
		DateTimeOffset collected;
		using (var serviceScope = Factory.Services.CreateScope())
		{
			var services = serviceScope.ServiceProvider;
			using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
			using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var year = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(Factory.SeededAcademicYearId);
			collected = new DateTimeOffset(year.StartDate.Year, year.StartDate.Month, 2, 12, 0, 0, TimeSpan.FromMinutes(330));
			var cash = new CashCollection(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), year.Id,
				Guid.NewGuid(), reference, null, reference, "Monthly activity payer", 1000m, collected,
				Guid.NewGuid(), null, null);
			cash.SpendAvailable(Guid.NewGuid(), "Later expense", 200m, collected.AddMonths(1));
			// Reproduce a compatible approved-held legacy source without generating a new immediate deposit.
			typeof(CashCollection).GetProperty(nameof(CashCollection.Status))!.SetValue(cash, 1);
			var deposit = cash.Dispatch(Guid.NewGuid(), Guid.NewGuid(), 500m, collected.AddMonths(1), "Legacy dispatch", null);
			var creditMonth = collected.AddMonths(3);
			var indiaMonthBoundary = new DateTimeOffset(creditMonth.Year, creditMonth.Month, 1, 0, 0, 0, TimeSpan.FromMinutes(330));
			cash.Credit(deposit.Id, Guid.NewGuid(), 100m, indiaMonthBoundary.AddSeconds(-1), "First credit", null);
			cash.Credit(deposit.Id, Guid.NewGuid(), 400m, indiaMonthBoundary, "Second credit", null);
			// Older history retains only the aggregate amount and its last known credit date.
			if (legacyWithoutCreditEvents) deposit.Credits.Clear();
			if (unknownLegacyCreditDate)
				typeof(CashDepositRecord).GetProperty(nameof(CashDepositRecord.CreditDate))!.SetValue(deposit, null);
			await services.GetRequiredService<IRepository<CashCollection, Guid>>().InsertAsync(cash, autoSave: true);
			await uow.CompleteAsync();
		}
		async Task<JsonElement> Read(DateTimeOffset? month)
		{
			var period = month.HasValue ? $"&month={month.Value.Month}&year={month.Value.Year}" : "";
			return await Client.GetFromJsonAsync<JsonElement>(
				$"/api/finance/cash-management?academicYearId={Factory.SeededAcademicYearId}&search={reference}{period}", JsonOptions);
		}
		var all = await Read(null);
		var available = all.GetProperty("availableCash").GetDecimal();
		AssertActivity(all, 1000m, 200m, 500m);
		all.GetProperty("summary").GetProperty("deposited").GetDecimal().ShouldBe(500m);
		foreach (var (offset, received, spent, deposited) in new[]
		{
			(0, 1000m, 0m, 0m), (1, 0m, 200m, 0m),
			(2, 0m, 0m, legacyWithoutCreditEvents ? 0m : 100m),
			(3, 0m, 0m, unknownLegacyCreditDate ? 0m : legacyWithoutCreditEvents ? 500m : 400m)
		})
		{
			var page = await Read(collected.AddMonths(offset));
			AssertActivity(page, received, spent, deposited);
			page.GetProperty("totalCount").GetInt32().ShouldBe(offset == 0 ? 1 : 0);
			page.GetProperty("summary").GetProperty("collected").GetDecimal().ShouldBe(offset == 0 ? 1000m : 0m);
			page.GetProperty("availableCash").GetDecimal().ShouldBe(available);
		}
		var noMatch = await Client.GetFromJsonAsync<JsonElement>(
			$"/api/finance/cash-management?academicYearId={Factory.SeededAcademicYearId}&search={reference}-absent", JsonOptions);
		AssertActivity(noMatch, 0m, 0m, 0m);
		noMatch.GetProperty("availableCash").GetDecimal().ShouldBe(available);

		var expenseDay = collected.AddMonths(1).ToString("yyyy-MM-dd");
		var query = $"academicYearId={Factory.SeededAcademicYearId}&search={reference}&startDate={expenseDay}&endDate={expenseDay}";
		var day = await Client.GetFromJsonAsync<JsonElement>($"/api/finance/cash-management?{query}", JsonOptions);
		AssertActivity(day, 0m, 200m, 0m);
		day.GetProperty("totalCount").GetInt32().ShouldBe(0);
		day.GetProperty("availableCash").GetDecimal().ShouldBe(available);
		var expensePage = await Client.GetFromJsonAsync<JsonElement>($"/api/finance/cash-management/expenses?{query}", JsonOptions);
		expensePage.GetProperty("totalCount").GetInt32().ShouldBe(1);
		var expense = expensePage.GetProperty("items")[0];
		expense.GetProperty("amount").GetDecimal().ShouldBe(200m);
		expense.GetProperty("sourceReference").GetString().ShouldBe(reference);
	}

	[Fact]
	public async Task Cash_page_is_slim_and_preserves_latest_historical_bank_snapshot_and_complete_options()
	{
		var reference = $"READS-{Guid.NewGuid():N}";
		var latestBank = Guid.NewGuid();
		var expectedRemaining = 0m;
		using (var serviceScope = Factory.Services.CreateScope())
		{
			var services = serviceScope.ServiceProvider;
			using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
			using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var year = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(Factory.SeededAcademicYearId);
			var collected = new DateTimeOffset(year.StartDate.Year, year.StartDate.Month, 2, 12, 0, 0, TimeSpan.FromMinutes(330));
			var repository = services.GetRequiredService<IRepository<CashCollection, Guid>>();
			for (var index = 0; index < 105; index++)
			{
				var cash = new CashCollection(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), year.Id,
					Guid.NewGuid(), reference + index, null, reference + index, "Read projection payer", 100.03m,
					collected, Guid.NewGuid(), null, null);
				cash.SpendAvailable(Guid.NewGuid(), "Exact paise expense", 0.01m, collected.AddDays(1));
				if (index == 0)
				{
					typeof(CashCollection).GetProperty(nameof(CashCollection.Status))!.SetValue(cash, 1);
					var older = cash.Dispatch(Guid.NewGuid(), Guid.NewGuid(), 0.01m, collected.AddDays(2), "Older", null);
					var latest = cash.Dispatch(Guid.NewGuid(), Guid.NewGuid(), 0.01m, collected.AddDays(3), "Latest", null);
					cash.ExtraProperties["DepositBank:" + older.Id.ToString("N")] = JsonSerializer.Serialize(new CashBankOption(Guid.NewGuid(), "Earlier snapshot"));
					cash.ExtraProperties["DepositBank:" + latest.Id.ToString("N")] = JsonSerializer.Serialize(new CashBankOption(latestBank, "Latest snapshot"));
					cash.SaveCommand(Guid.NewGuid(), "read-projection", new string('x', 50000));
				}
				expectedRemaining += cash.RemainingAmount;
				await repository.InsertAsync(cash);
			}
			await uow.CompleteAsync();
		}
		var page = await Client.GetFromJsonAsync<JsonElement>(
			$"/api/finance/cash-management?academicYearId={Factory.SeededAcademicYearId}&search={reference}&pageSize=1&status=1", JsonOptions);
		page.GetProperty("totalCount").GetInt32().ShouldBe(1);
		page.GetProperty("statusCounts").GetProperty("all").GetInt32().ShouldBe(105);
		page.GetProperty("statusCounts").GetProperty("pending").GetInt32().ShouldBe(104);
		page.GetProperty("summary").GetProperty("remaining").GetDecimal().ShouldBe(100.00m);
		var row = page.GetProperty("items")[0];
		row.GetProperty("bankAccountId").GetGuid().ShouldBe(latestBank);
		row.GetProperty("bankLabel").GetString().ShouldBe("Latest snapshot");
		row.TryGetProperty("deposits", out _).ShouldBeFalse("The register must expose only CashRow fields.");
		row.TryGetProperty("expenses", out _).ShouldBeFalse();
		var all = await Client.GetFromJsonAsync<JsonElement>(
			$"/api/finance/cash-management?academicYearId={Factory.SeededAcademicYearId}&search={reference}&pageSize=1", JsonOptions);
		all.GetProperty("summary").GetProperty("remaining").GetDecimal().ShouldBe(expectedRemaining);
		all.GetProperty("summary").GetProperty("collected").GetDecimal().ShouldBe(10503.15m);
		var options = await Client.GetFromJsonAsync<JsonElement>(
			$"/api/finance/cash-management/options?academicYearId={Factory.SeededAcademicYearId}&operation=expense", JsonOptions);
		var matching = 0;
		foreach (var option in options.GetProperty("cashSources").EnumerateArray())
			if (option.GetProperty("label").GetString()!.StartsWith(reference, StringComparison.Ordinal)) matching++;
		matching.ShouldBe(105, "The complete source selector must include candidates beyond the first page.");
	}

	private static void AssertActivity(JsonElement page, decimal collected, decimal spent, decimal deposited)
	{
		page.TryGetProperty("activitySummary", out var activity).ShouldBeTrue("The cash page must expose event-month activity separately from source-row balances.");
		activity.GetProperty("collected").GetDecimal().ShouldBe(collected);
		activity.GetProperty("spent").GetDecimal().ShouldBe(spent);
		activity.GetProperty("deposited").GetDecimal().ShouldBe(deposited);
	}
}
