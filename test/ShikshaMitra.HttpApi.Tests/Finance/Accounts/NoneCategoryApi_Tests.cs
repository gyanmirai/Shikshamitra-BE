using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Activity;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

[Collection(FinanceRoutingApiTestCollection.Name)]
public class NoneCategoryApi_Tests(FinanceRoutingApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData("income-categories", "fee-head", "incomeCategories")]
	[InlineData("expense-categories", "refund-policy", "expenseCategories")]
	public async Task None_is_stored_and_protected_from_master_changes(string route, string operation, string options)
	{
		await EnsureAcademicScopeAsync();
		var first = await GetNoneAsync(route);
		var second = await GetNoneAsync(route);
		var id = first.GetProperty("id").GetGuid();
		id.ShouldNotBe(Guid.Empty);
		second.GetProperty("id").GetGuid().ShouldBe(id);
		first.GetProperty("isActive").GetBoolean().ShouldBeTrue();
		first.GetProperty("canDelete").GetBoolean().ShouldBeFalse();
		var master = await Client.GetFromJsonAsync<JsonElement>($"/api/{route}?academicYearId={Factory.SeededAcademicYearId}&pageSize=100");
		master.GetProperty("items").EnumerateArray().Any(x => x.GetProperty("name").GetString() == "None").ShouldBeFalse();
		var routing = await Client.GetFromJsonAsync<JsonElement>($"/api/finance-routing-options?academicYearId={Factory.SeededAcademicYearId}&operation={operation}");
		routing.GetProperty(options).EnumerateArray().Single(x => x.GetProperty("label").GetString() == "None")
			.GetProperty("id").GetGuid().ShouldBe(id);
		await WithTenantAsync(async services =>
		{
			var stored = await services.GetRequiredService<IRepository<TransactionCategory, Guid>>().GetAsync(id);
			stored.TenantId.ShouldBe(Guid.Parse(TestAuthHandler.TestTenantId));
			stored.AcademicYearId.ShouldBe(Factory.SeededAcademicYearId);
			stored.Name.ShouldBe("None");
			await services.GetRequiredService<FinanceRoutingManager>().RequireCategoryAsync(
				Factory.SeededAcademicYearId, id, route == "income-categories" ? TransactionType.Income : TransactionType.Expense);
		});
		using var editor = Factory.CreateClient();
		var permissionRoot = route == "income-categories" ? "Finance.Accounts.Settings.IncomeCategory" : "Finance.Accounts.Settings.ExpenseCategory";
		editor.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			$"{permissionRoot}.Delete,{permissionRoot}.Edit,{permissionRoot}.ChangeStatus");
		var deletion = await editor.DeleteAsync($"/api/{route}/{id}");
		deletion.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await deletion.Content.ReadAsStringAsync()).ShouldContain("Finance:Accounts:Settings:SystemCategoryReadOnly");
		var update = await editor.PutAsJsonAsync($"/api/{route}/{id}", new { name = "Changed None", tags = Array.Empty<string>(), concurrencyStamp = first.GetProperty("concurrencyStamp").GetString() });
		update.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await update.Content.ReadAsStringAsync()).ShouldContain("Finance:Accounts:Settings:SystemCategoryReadOnly");
		var status = await editor.PatchAsJsonAsync($"/api/{route}/{id}/status", new { isActive = false, deactivationReason = "Not applicable", concurrencyStamp = first.GetProperty("concurrencyStamp").GetString() });
		status.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await status.Content.ReadAsStringAsync()).ShouldContain("Finance:Accounts:Settings:SystemCategoryReadOnly");
	}

	[Theory]
	[InlineData("income-entry", "incomeCategories", ShikshaMitraSubmodulePermissions.Finance.Accounts_IncomeTracking.Add)]
	[InlineData("expense-entry", "expenseCategories", ShikshaMitraSubmodulePermissions.Finance.Accounts_ExpenseTracking.Add)]
	[InlineData("cash-expense", "expenseCategories", ShikshaMitraSubmodulePermissions.Finance.Accounts_ExpenseTracking.Add)]
	[InlineData("collection", "incomeCategories", ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add)]
	[InlineData("refund", "expenseCategories", ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve)]
	[InlineData("collection-policy", "incomeCategories", ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Add)]
	[InlineData("refund-policy", "expenseCategories", ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.Add)]
	[InlineData("fee-head", "incomeCategories", ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeHead.Add)]
	[InlineData("charge-category", "incomeCategories", ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ChargeCategory.Add)]
	public async Task Every_category_lookup_offers_None_first(string operation, string options, string permission)
	{
		await EnsureAcademicScopeAsync();
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permission);
		var result = await client.GetFromJsonAsync<JsonElement>($"/api/finance-routing-options?academicYearId={Factory.SeededAcademicYearId}&operation={operation}");
		var choices = result.GetProperty(options).EnumerateArray().ToArray();
		choices.Any(x => x.GetProperty("label").GetString()!.StartsWith("AAA visible")).ShouldBeTrue();
		choices[0].GetProperty("label").GetString().ShouldBe("None");
		choices[0].GetProperty("id").GetGuid().ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public async Task New_year_defaults_are_idempotent_and_do_not_reuse_other_year_category_ids()
	{
		Guid newYearId = Guid.Empty;
		await WithTenantAsync(async services =>
		{
			var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync(
				$"None defaults {Guid.NewGuid():N}", new DateTime(2035, 1, 1), new DateTime(2035, 12, 31));
			await services.GetRequiredService<IAcademicYearRepository>().InsertAsync(year);
			newYearId = year.Id;
		});
		await WithTenantAsync(async services =>
		{
			var year = await services.GetRequiredService<IAcademicYearRepository>().GetAsync(newYearId);
			var categories = services.GetRequiredService<IRepository<TransactionCategory, Guid>>();
			var original = await categories.GetListAsync(x => x.AcademicYearId == newYearId);
			original.Count.ShouldBe(2);
			await services.GetRequiredService<NoneCategoryInitializer>().InitializeAsync(year);
			await services.GetRequiredService<NoneCategoryInitializer>().InitializeAsync(year);
			var repeated = await categories.GetListAsync(x => x.AcademicYearId == newYearId);
			repeated.Select(x => x.Id).OrderBy(x => x).ShouldBe(original.Select(x => x.Id).OrderBy(x => x));
			original.All(x => x.Name == "None" && x.IsActive && x.TenantId == year.TenantId).ShouldBeTrue();
			var income = original.Single(x => x.CategoryType == TransactionType.Income);
			var invalid = await Should.ThrowAsync<BusinessException>(() => services.GetRequiredService<FinanceRoutingManager>()
				.RequireCategoryAsync(Factory.SeededAcademicYearId, income.Id, TransactionType.Income));
			invalid.Code.ShouldBe("Finance:Routing:InvalidCategory");
		});
	}

	[Fact]
	public async Task A_fee_head_can_save_the_stored_None_income_category()
	{
		await EnsureAcademicScopeAsync();
		var none = await GetNoneAsync("income-categories");
		var id = none.GetProperty("id").GetGuid();
		var response = await Client.PostAsJsonAsync($"/api/fee-categories?academicYearId={Factory.SeededAcademicYearId}", new
		{
			name = $"None routing {Guid.NewGuid():N}",
			description = "Synthetic None routing category",
			feeHeads = new[] { new { name = "Uncategorized fee", description = "Synthetic fee head", incomeCategoryId = id, isConcessionApplied = false } }
		});
		response.EnsureSuccessStatusCode();
		var saved = await response.Content.ReadFromJsonAsync<JsonElement>();
		saved.GetProperty("feeHeads")[0].GetProperty("incomeCategoryId").GetGuid().ShouldBe(id);
	}

	[Theory]
	[InlineData("income", "income-categories", "incomes", TransactionType.Income)]
	[InlineData("expense", "expense-categories", "expenses", TransactionType.Expense)]
	public async Task None_is_absent_from_registers_activity_details_and_legacy_lists(
		string direction, string categoryRoute, string legacyRoute, TransactionType type)
	{
		await EnsureAcademicScopeAsync();
		var noneId = (await GetNoneAsync(categoryRoute)).GetProperty("id").GetGuid();
		var marker = $"NE-{Guid.NewGuid():N}";
		var noneEntryId = Guid.NewGuid();
		var regularEntryId = Guid.NewGuid();
		await WithTenantAsync(async services =>
		{
			var tenant = Guid.Parse(TestAuthHandler.TestTenantId);
			var regular = new TransactionCategory(Guid.NewGuid(), tenant, Factory.SeededAcademicYearId, type, marker, null);
			await services.GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(regular, autoSave: true);
			var year = await services.GetRequiredService<IAcademicYearRepository>().GetAsync(Factory.SeededAcademicYearId);
			var at = year.StartDate.Date.AddHours(12);
			var repository = services.GetRequiredService<IRepository<Transaction, Guid>>();
			await repository.InsertAsync(Transaction.Automatic(noneEntryId, tenant, year.Id, noneId, "None",
				null, null, marker, 25m, 3, at, type, $"none-test:{noneEntryId:N}", noneEntryId, null), autoSave: true);
			var manual = new Transaction(regularEntryId, tenant, year.Id, regular.Id, marker, null, 75m, 0, at, transactionType: type);
			manual.SetManualRouting(regular.Name, null, null, null);
			await repository.InsertAsync(manual, autoSave: true);
		});
		var page = await Client.GetFromJsonAsync<AccountsActivityPageDto>(
			$"/api/accounts/register?academicYearId={Factory.SeededAcademicYearId}&direction={direction}&search={marker}", JsonOptions);
		page!.TotalCount.ShouldBe(1);
		page.Items.Single().Id.ShouldBe(regularEntryId);
		page.Items.Sum(x => x.Amount).ShouldBe(75m);
		page.Categories.ShouldNotContain(x => x.Id == noneId);
		var activity = await Client.GetFromJsonAsync<AccountsActivityPageDto>(
			$"/api/accounts/activity?academicYearId={Factory.SeededAcademicYearId}&search={marker}", JsonOptions);
		activity!.Items.ShouldNotContain(x => x.Id == noneEntryId);
		var legacy = await Client.GetFromJsonAsync<JsonElement>($"/api/{legacyRoute}?academicYearId={Factory.SeededAcademicYearId}&search={marker}");
		legacy.GetProperty("totalCount").GetInt32().ShouldBe(1);
		var source = direction == "income" ? "collection" : "refund";
		(await Client.GetAsync($"/api/accounts/register/{source}/{noneEntryId}?academicYearId={Factory.SeededAcademicYearId}"))
			.IsSuccessStatusCode.ShouldBeFalse();
		(await Client.GetAsync($"/api/{legacyRoute}/{noneEntryId}")).IsSuccessStatusCode.ShouldBeFalse();
		await WithTenantAsync(async services =>
		{
			var stored = await services.GetRequiredService<IRepository<Transaction, Guid>>().GetAsync(noneEntryId);
			stored.IsConfirmed.ShouldBeTrue();
			stored.Amount.ShouldBe(25m);
		});
	}

	[Theory]
	[InlineData("income", "income-categories", "incomes")]
	[InlineData("expense", "expense-categories", "expenses")]
	public async Task Manual_None_can_save_and_replay_but_missing_or_wrong_type_category_is_rejected(
		string direction, string categoryRoute, string route)
	{
		await EnsureAcademicScopeAsync();
		var none = (await GetNoneAsync(categoryRoute)).GetProperty("id").GetGuid();
		var opposite = (await GetNoneAsync(direction == "income" ? "expense-categories" : "income-categories")).GetProperty("id").GetGuid();
		var bankId = Guid.NewGuid();
		DateTime at = default;
		await WithTenantAsync(async services =>
		{
			var year = await services.GetRequiredService<IAcademicYearRepository>().GetAsync(Factory.SeededAcademicYearId);
			at = DateTime.SpecifyKind(year.StartDate.Date.AddHours(12), DateTimeKind.Utc);
			await services.GetRequiredService<IRepository<BankAccount, Guid>>().InsertAsync(new BankAccount(bankId,
				year.Id, "None routing bank", "Synthetic branch", null, "School",
				Enum.GetValues<ShikshaMitra.Finance.Accounts.Settings.BankAccountType>().First(),
				BitConverter.ToUInt64(bankId.ToByteArray(), 0).ToString(), "None testing", "TEST0001234", null,
				tenantId: Guid.Parse(TestAuthHandler.TestTenantId)), autoSave: true);
		});
		var key = Guid.NewGuid();
		var marker = $"None manual {Guid.NewGuid():N}";
		object Body(Guid category, Guid requestKey) => new { categoryId = category, idempotencyKey = requestKey,
			name = marker, counterparty = "Synthetic payer", amount = 12.34m, paymentMethod = 3,
			bankAccountId = bankId, paymentDate = at, categoryTags = Array.Empty<string>() };
		for (var attempt = 0; attempt < 2; attempt++)
		{
			var saved = await Client.PostAsJsonAsync($"/api/{route}?academicYearId={Factory.SeededAcademicYearId}", Body(none, key));
			saved.StatusCode.ShouldBe(HttpStatusCode.Created, await saved.Content.ReadAsStringAsync());
			(await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid().ShouldBe(key);
		}
		var listed = await Client.GetFromJsonAsync<AccountsActivityPageDto>(
			$"/api/accounts/register?academicYearId={Factory.SeededAcademicYearId}&direction={direction}&search={marker}", JsonOptions);
		listed!.TotalCount.ShouldBe(0);
		foreach (var invalid in new[] { Guid.Empty, opposite })
		{
			var result = await Client.PostAsJsonAsync($"/api/{route}?academicYearId={Factory.SeededAcademicYearId}", Body(invalid, Guid.NewGuid()));
			result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		}
		await WithTenantAsync(async services =>
		{
			var entries = await services.GetRequiredService<IRepository<Transaction, Guid>>().GetListAsync(x => x.Name == marker);
			entries.ShouldHaveSingleItem().Amount.ShouldBe(12.34m);
		});
	}

	private async Task<JsonElement> GetNoneAsync(string route)
	{
		var operation = route == "income-categories" ? "fee-head" : "refund-policy";
		var options = route == "income-categories" ? "incomeCategories" : "expenseCategories";
		var result = await Client.GetFromJsonAsync<JsonElement>($"/api/finance-routing-options?academicYearId={Factory.SeededAcademicYearId}&operation={operation}");
		var id = result.GetProperty(options)[0].GetProperty("id").GetGuid();
		return await Client.GetFromJsonAsync<JsonElement>($"/api/{route}/{id}");
	}

	private Task EnsureAcademicScopeAsync() => WithTenantAsync(async services =>
	{
		var students = services.GetRequiredService<IRepository<Student, Guid>>();
		if (!await students.AnyAsync(x => x.AcademicYearId == Factory.SeededAcademicYearId))
			await students.InsertAsync(new Student(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), "None", null, "Test",
				Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId,
				Factory.SeededMediumStageId, Factory.SeededStandardId, Factory.SeededDivisionId), autoSave: true);
		var categories = services.GetRequiredService<IRepository<TransactionCategory, Guid>>();
		foreach (var type in new[] { TransactionType.Income, TransactionType.Expense })
		{
			var code = $"NONE-SCOPE-{type}".ToUpperInvariant();
			if (!await categories.AnyAsync(x => x.AcademicYearId == Factory.SeededAcademicYearId && x.Code == code))
				await categories.InsertAsync(new TransactionCategory(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId),
					Factory.SeededAcademicYearId, type, $"AAA visible {type}", "Synthetic ordering control", code: code), autoSave: true);
		}
	});

	private async Task WithTenantAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using (services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			await action(services);
			await uow.CompleteAsync();
		}
	}
}
