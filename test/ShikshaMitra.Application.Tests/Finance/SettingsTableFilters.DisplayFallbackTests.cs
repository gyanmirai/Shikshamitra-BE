using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using Shouldly;
using Xunit;
using Concession = ShikshaMitra.Finance.Fees.Settings.ConcessionTypes.ConcessionType;

namespace ShikshaMitra.Finance.Settings;

public partial class SettingsTableFilters_Tests
{
	[Theory]
	[InlineData("bank", "branchAddress")]
	[InlineData("income", "description")]
	[InlineData("expense", "description")]
	[InlineData("charge", "description")]
	[InlineData("concession", "description")]
	[InlineData("concession", "documents")]
	[InlineData("concession", "deactivationReason")]
	public async Task Displayed_dash_matches_null_and_empty_values_before_count_and_page_and_clears(string screen, string column)
	{
		using var identityScope = BeginPrincipal();
		var year = await YearAsync();
		var nullId = await SeedBlankDisplayAsync(screen, year, "Null", null, 1);
		var emptyId = await SeedBlankDisplayAsync(screen, year, "Empty", "", 2);
		await SeedAsync(screen, year, "Populated", 3, active: column != "deactivationReason");

		var filter = Filter(column, "-");
		var first = await ListAsync(screen, year, filter);
		var second = await ListAsync(screen, year, filter, page: 2);
		first.Total.ShouldBe(2);
		second.Total.ShouldBe(2);
		first.Ids.Concat(second.Ids).OrderBy(x => x).ShouldBe(new[] { nullId, emptyId }.OrderBy(x => x));
		(await ListAsync(screen, year, filter, page: 3)).Ids.ShouldBeEmpty();
		(await ListAsync(screen, year, Filter(column, "", "equals"))).Total.ShouldBe(0);
		(await ListAsync(screen, year, filter, search: "Null")).Ids.Single().ShouldBe(nullId);
		(await ListAsync(screen, year, "[]")).Total.ShouldBe(3);
		(await ListAsync(screen, year, null)).Total.ShouldBe(3);
	}

	[Fact]
	public async Task Bank_purpose_keeps_its_raw_empty_display_instead_of_a_dash()
	{
		using var identityScope = BeginPrincipal();
		var year = await YearAsync();
		await SeedBlankDisplayAsync("bank", year, "Null", null, 1);
		await SeedBlankDisplayAsync("bank", year, "Empty", "", 2);
		await SeedAsync("bank", year, "Populated", 3);
		(await ListAsync("bank", year, Filter("purpose", ""))).Total.ShouldBe(2);
		(await ListAsync("bank", year, Filter("purpose", "-"))).Total.ShouldBe(0);
		(await ListAsync("bank", year, null)).Total.ShouldBe(3);
	}

	private async Task<Guid> SeedBlankDisplayAsync(string screen, Guid year, string name, string? value, int number)
	{
		var id = Guid.NewGuid();
		switch (screen)
		{
			case "bank":
				var bank = new BankAccount(id, year, name, "Branch", value, "Holder", BankAccountType.Savings,
					$"87654321100{number}", value, "ABCD0123456", null, tenantId: TenantId);
				// Persist a legacy empty string as well as the current domain's normalized null.
				typeof(BankAccount).GetProperty(nameof(BankAccount.BranchAddress))!.SetValue(bank, value);
				typeof(BankAccount).GetProperty(nameof(BankAccount.Purpose))!.SetValue(bank, value);
				await InsertAsync(bank);
				break;
			case "income": case "expense":
				var category = new TransactionCategory(id, TenantId, year,
					screen == "income" ? TransactionType.Income : TransactionType.Expense, name, value);
				typeof(TransactionCategory).GetProperty(nameof(TransactionCategory.Description))!.SetValue(category, value);
				await InsertAsync(category);
				break;
			case "charge":
				await InsertAsync(new ChargeCategory(id, TenantId, year, name, true, value));
				break;
			case "concession":
				var concession = new Concession(id, TenantId, year, name, value);
				typeof(Concession).GetProperty(nameof(Concession.Description))!.SetValue(concession, value);
				typeof(Concession).GetProperty(nameof(Concession.DeactivationReason))!.SetValue(concession, value);
				await InsertAsync(concession);
				break;
		}
		return id;
	}
}
