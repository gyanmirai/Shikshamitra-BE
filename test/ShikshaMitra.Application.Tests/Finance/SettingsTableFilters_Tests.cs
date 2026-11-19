using System;
using System.Collections.Generic;
using System.Security.Claims;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Users;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.ChargeCategories;
using ShikshaMitra.Finance.Fees.Settings.ConcessionType;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Validation;
using Xunit;
using Concession = ShikshaMitra.Finance.Fees.Settings.ConcessionTypes.ConcessionType;

namespace ShikshaMitra.Finance.Settings;

public partial class SettingsTableFilters_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Theory]
	[InlineData("income")]
	[InlineData("expense")]
	[InlineData("bank")]
	[InlineData("charge")]
	[InlineData("concession")]
	[InlineData("structure")]
	public async Task Every_list_filters_before_count_and_second_page_and_clears(string screen)
	{
		using var tenant = screen == "structure" ? GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()) : null;
		using var identityScope = BeginPrincipal();
		var year = await YearAsync();
		var alpha = await SeedAsync(screen, year, "Alpha", 1);
		var beta = await SeedAsync(screen, year, "Beta", 2);
		await SeedAsync(screen, year, "Other", 3);
		await SeedAsync(screen, await YearAsync(), "Alpha", 1);
		var name = screen == "bank" ? "bankName" : "name";
		var criteria = JsonSerializer.Serialize(new[] { new { column = name, type = "equals", value = "Alpha" }, new { column = name, type = "equals", value = "Beta" } });
		var first = await ListAsync(screen, year, criteria);
		var second = await ListAsync(screen, year, criteria, page: 2);
		first.Total.ShouldBe(2);
		second.Total.ShouldBe(2);
		first.Ids.Concat(second.Ids).OrderBy(x => x).ShouldBe(new[] { alpha, beta }.OrderBy(x => x));
		(await ListAsync(screen, year, criteria, page: 3)).Ids.ShouldBeEmpty();
		(await ListAsync(screen, year, Filter(name, "Missing"))).Total.ShouldBe(0);
		(await ListAsync(screen, year, criteria, search: "Alpha")).Ids.Single().ShouldBe(alpha);
		(await ListAsync(screen, year, "[]")).Total.ShouldBe(3);
		(await ListAsync(screen, year, null)).Total.ShouldBe(3);
	}

	[Theory]
	[InlineData("contains", "LPH")]
	[InlineData("equals", "aLPHa")]
	[InlineData("startsWith", "al")]
	[InlineData("endsWith", "HA")]
	public async Task Text_operators_are_case_insensitive_and_combine_with_enum_status(string operation, string value)
	{
		using var identityScope = BeginPrincipal();
		var year = await YearAsync();
		await SeedAsync("income", year, "Alpha", 1);
		await SeedAsync("income", year, "Other", 2);
		var criteria = $$"""[{"column":"name","type":"{{operation}}","value":"{{value}}"},{"column":"isActive","type":"equals","value":1}]""";
		(await ListAsync("income", year, criteria)).Total.ShouldBe(1);
		(await ListAsync("income", year, criteria.Replace("\"value\":1", "\"value\":0"))).Total.ShouldBe(0);
	}

	[Theory]
	[InlineData("income")]
	[InlineData("expense")]
	[InlineData("bank")]
	[InlineData("charge")]
	[InlineData("concession")]
	public async Task Status_header_intersects_existing_status_switch(string screen)
	{
		using var identityScope = BeginPrincipal();
		var year = await YearAsync();
		await SeedAsync(screen, year, "Alpha", 1);
		await SeedAsync(screen, year, "Archived", 2, active: false);
		const string inactive = """[{"column":"isActive","type":"equals","value":0}]""";
		(await ListAsync(screen, year, inactive)).Total.ShouldBe(1);
		(await ListAsync(screen, year, inactive, onlyActive: true)).Total.ShouldBe(0);
	}

	[Fact]
	public async Task Banks_match_only_visible_masks_and_upi_fallback()
	{
		using var identityScope = BeginPrincipal();
		var year = await YearAsync();
		var id = await SeedAsync("bank", year, "Alpha", 1);
		await SeedAsync("bank", year, "Other", 2);
		foreach (var pair in new[] { ("bankName", "Alpha"), ("branchName", "Branch 1"), ("branchAddress", "Address 1"), ("accountHolderName", "Holder 1"), ("purpose", "Purpose 1"), ("maskedAccountNumber", "********1001"), ("maskedUpiId", "***@bank1"), ("ifscCode", "ABCD*******") })
		{
			var result = await ListAsync("bank", year, Filter(pair.Item1, pair.Item2));
			result.Ids.ShouldContain(id);
		}
		foreach (var pair in new[] { ("maskedAccountNumber", "76543210"), ("maskedUpiId", "hidden1"), ("ifscCode", "0123456") })
			(await ListAsync("bank", year, Filter(pair.Item1, pair.Item2, "contains"))).Total.ShouldBe(0);
		(await ListAsync("bank", year, """[{"column":"accountType","type":"equals","value":1},{"column":"bankName","type":"equals","value":"Alpha"}]""")).Total.ShouldBe(1);
		(await ListAsync("bank", year, Filter("maskedUpiId", "-"))).Total.ShouldBe(1);
		var response = await GetRequiredService<IBankAccountAppService>().GetListAsync(year, filters: Filter("bankName", "Alpha"));
		response.Items.Single().AccountNumber.ShouldBeNull();
		response.Items.Single().UpiId.ShouldBeNull();
	}

	[Fact]
	public async Task Concession_documents_are_names_in_response_order_and_not_ids()
	{
		using var identityScope = BeginPrincipal();
		var year = await YearAsync();
		var id = await SeedAsync("concession", year, "Alpha", 1);
		await SeedAsync("concession", year, "Other", 2, active: false);
		var service = GetRequiredService<IConcessionTypeAppService>();
		var row = await service.GetAsync(id);
		var displayed = string.Join(", ", row.Documents.Select(x => x.DocumentName));
		(await ListAsync("concession", year, Filter("documents", displayed))).Ids.Single().ShouldBe(id);
		(await ListAsync("concession", year, Filter("documents", row.Documents.First().Id.ToString()))).Total.ShouldBe(0);
		(await ListAsync("concession", year, Filter("description", "Description 1"))).Ids.Single().ShouldBe(id);
		(await ListAsync("concession", year, Filter("deactivationReason", "Retired"))).Total.ShouldBe(1);
	}

	[Fact]
	public async Task Charge_routing_name_and_approval_filters_use_displayed_values()
	{
		using var identityScope = BeginPrincipal();
		var year = await YearAsync();
		var id = await SeedAsync("charge", year, "Alpha", 1);
		await SeedAsync("charge", year, "Other", 2);
		(await ListAsync("charge", year, Filter("incomeCategoryName", "Income 1"))).Ids.Single().ShouldBe(id);
		(await ListAsync("charge", year, """[{"column":"incomeCategoryName","type":"contains","value":"Income"},{"column":"isApprovalRequired","type":"equals","value":1}]""")).Ids.Single().ShouldBe(id);
		(await ListAsync("charge", year, Filter("description", "Description 1"))).Ids.Single().ShouldBe(id);
		var missing = new ChargeCategory(Guid.NewGuid(), TenantId, year, "No mapping", true, null);
		await InsertAsync(missing);
		(await ListAsync("charge", year, Filter("incomeCategoryName", "Income Category required"))).Ids.Single().ShouldBe(missing.Id);
	}

	[Fact]
	public async Task Structure_uses_board_medium_and_configured_fraction_as_text()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		using var identityScope = BeginPrincipal();
		var year = await YearAsync();
		var id = await SeedAsync("structure", year, "Alpha", 1);
		await SeedAsync("structure", year, "Other", 2);
		(await ListAsync("structure", year, Filter("boardMediumValue", "CBSE/Medium 1"))).Ids.Single().ShouldBe(id);
		(await ListAsync("structure", year, """[{"column":"totalClassValue","type":"equals","value":"0/1"},{"column":"name","type":"equals","value":"Alpha"}]""")).Ids.Single().ShouldBe(id);
		(await ListAsync("structure", year, Filter("totalClassValue", "0/2"))).Total.ShouldBe(0);
		await Should.ThrowAsync<AbpValidationException>(() => ListAsync("structure", year, """[{"column":"totalClassValue","type":"greaterThan","value":0}]"""));
	}

	[Theory]
	[InlineData("income")]
	[InlineData("expense")]
	[InlineData("bank")]
	[InlineData("charge")]
	[InlineData("concession")]
	[InlineData("structure")]
	public async Task Invalid_criteria_are_rejected_even_when_no_rows_match(string screen)
	{
		using var identityScope = BeginPrincipal();
		foreach (var invalid in new[] { "broken", "null", "[null]", "{}", "[{\"column\":null}]", "[{\"column\":\"secret\",\"type\":\"equals\",\"value\":\"x\"}]", "[{\"column\":\"name\",\"type\":\"unknown\",\"value\":\"x\"}]", "[{\"column\":\"isActive\",\"type\":\"equals\",\"value\":2}]", "[{\"column\":\"name\",\"type\":\"equals\",\"value\":null}]" })
			await Should.ThrowAsync<AbpValidationException>(() => ListAsync(screen, Guid.NewGuid(), invalid));
	}

	private IDisposable BeginPrincipal() => GetRequiredService<ICurrentPrincipalAccessor>().Change(
		new ClaimsPrincipal(new ClaimsIdentity([new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()),
			new Claim(AbpClaimTypes.TenantId, (TenantId ?? Guid.Empty).ToString())], "Test")));

	private Guid? TenantId => GetRequiredService<ICurrentTenant>().Id;
	private Task<T> InsertAsync<T>(T entity) where T : class, IEntity<Guid> => GetRequiredService<IRepository<T, Guid>>().InsertAsync(entity, autoSave: true);
	private async Task<Guid> YearAsync()
	{
		var userId = GetRequiredService<ICurrentUser>().Id!.Value;
		var users = GetRequiredService<IRepository<IdentityUser, Guid>>();
		if (await users.FindAsync(userId) is null)
		{
			await InsertAsync(new IdentityUser(userId, $"filter-{userId:N}", $"{userId:N}@example.test", TenantId));
			await InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TenantId, userId: userId));
		}
		var id = Guid.NewGuid();
		await InsertAsync(new AcademicYear(id, TenantId, $"Filters {id:N}", new DateTime(2026, 1, 1), new DateTime(2027, 12, 31)));
		return id;
	}
	private async Task<Guid> SeedAsync(string screen, Guid year, string name, int number, bool active = true)
	{
		var id = Guid.NewGuid();
		switch (screen)
		{
			case "income": case "expense":
				await InsertAsync(new TransactionCategory(id, TenantId, year, screen == "income" ? TransactionType.Income : TransactionType.Expense, name, $"Description {number}", isActive: active));
				break;
			case "bank":
				await InsertAsync(new BankAccount(id, year, name, $"Branch {number}", $"Address {number}", $"Holder {number}", number == 1 ? BankAccountType.Current : BankAccountType.Savings, $"76543210100{number}", $"Purpose {number}", "ABCD0123456", number == 1 ? "hidden1@bank1" : null, active, TenantId));
				break;
			case "concession":
				var concession = new Concession(id, TenantId, year, name, $"Description {number}");
				concession.AddDocument($"Proof {number}", 1024, [DocumentType.Pdf]);
				concession.AddDocument($"Evidence {number}", 1024, [DocumentType.Pdf]);
				if (!active) concession.Deactivate("Retired");
				await InsertAsync(concession);
				break;
			case "charge":
				var income = new TransactionCategory(Guid.NewGuid(), TenantId, year, TransactionType.Income, $"Income {number}", null);
				await InsertAsync(income);
				var charge = new ChargeCategory(id, TenantId, year, name, number == 1, $"Description {number}", active);
				charge.SetAccountingMapping(income.Id);
				await InsertAsync(charge);
				break;
			case "structure":
				var board = new EducationBoard(Guid.NewGuid(), TenantId, EducationBoardType.CBSE, year, new DateTime(2026, 1, 1), new DateTime(2027, 12, 31));
				await InsertAsync(board);
				var medium = new EducationMedium(Guid.NewGuid(), TenantId, $"Medium {number}", board.Id);
				await InsertAsync(medium);
				var stage = new EducationMediumStage(Guid.NewGuid(), TenantId, "Primary", null, medium.Id);
				await InsertAsync(stage);
				await InsertAsync(new Standard(Guid.NewGuid(), TenantId, "Class 1", 1, stage.Id, null));
				await InsertAsync(new FeeStructureMetadata(id, TenantId, year, board.Id, medium.Id, null, name));
				break;
		}
		return id;
	}
	private static string Filter(string column, string value, string type = "equals") => JsonSerializer.Serialize(new[] { new { column, type, value } });
	private async Task<(long Total, Guid[] Ids)> ListAsync(string screen, Guid year, string? filters, int page = 1, string? search = null, bool onlyActive = false)
	{
		switch (screen)
		{
			case "income": case "expense":
				var categories = await GetRequiredService<ITransactionCategoryAppService>().GetListAsync(year, screen == "income" ? TransactionType.Income : TransactionType.Expense, page, 1, search, !onlyActive, filters);
				return (categories.TotalCount, categories.Items.Select(x => x.Id).ToArray());
			case "bank":
				var banks = await GetRequiredService<IBankAccountAppService>().GetListAsync(year, page, 1, search, !onlyActive, filters);
				return (banks.TotalCount, banks.Items.Select(x => x.Id).ToArray());
			case "charge":
				var charges = await GetRequiredService<IChargeCategoryAppService>().GetListAsync(year, page, 1, search, onlyActive ? true : null, filters);
				return (charges.TotalCount, charges.Items.Select(x => x.Id).ToArray());
			case "concession":
				var concessions = await GetRequiredService<IConcessionTypeAppService>().GetListAsync(year, page - 1, 1, search, !onlyActive, null, filters);
				return (concessions.TotalCount, concessions.Items.Select(x => x.Id).ToArray());
			default:
				var structures = await GetRequiredService<IFeeStructureMetadataAppService>().GetListAsync(year, page, 1, search, filters);
				return (structures.TotalCount, structures.Items.Select(x => x.Id).ToArray());
		}
	}
}
