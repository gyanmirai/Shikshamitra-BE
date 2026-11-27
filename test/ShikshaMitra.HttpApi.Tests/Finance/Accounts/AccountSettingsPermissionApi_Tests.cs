using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

[Collection(FinanceRoutingApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class AccountSettingsPermissionApi_Tests(FinanceRoutingApiTestFactory factory)
	: ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData("income-categories", "IncomeCategory")]
	[InlineData("expense-categories", "ExpenseCategory")]
	[InlineData("bank-accounts", "BankAccount")]
	public async Task Settings_Module_Grant_Should_Not_Grant_View(string route, string setting)
	{
		using var client = RestrictedClient("ShikshaMitra.Module.Finance.Accounts.Settings");
		var response = await client.GetAsync($"/api/{route}?academicYearId={Factory.SeededAcademicYearId}");
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"The module grant must not grant {setting} view access.");
	}

	[Theory]
	[InlineData("income-categories", "IncomeCategory")]
	[InlineData("expense-categories", "ExpenseCategory")]
	[InlineData("bank-accounts", "BankAccount")]
	public async Task Add_Should_Not_Grant_Edit_Or_ChangeStatus(string route, string setting)
	{
		using var client = RestrictedClient($"Finance.Accounts.Settings.{setting}.Add");
		var id = Guid.NewGuid();
		var edit = await client.PutAsJsonAsync($"/api/{route}/{id}", new { });
		edit.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var status = await client.PatchAsJsonAsync($"/api/{route}/{id}/status", new { isActive = false });
		status.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Theory]
	[InlineData("income-categories", "IncomeCategory", "")]
	[InlineData("income-categories", "IncomeCategory", "?academicYearId=00000000-0000-0000-0000-000000000000")]
	[InlineData("expense-categories", "ExpenseCategory", "")]
	public async Task Add_With_No_Academic_Year_Returns_Bad_Request(string route, string setting, string query)
	{
		using var client = RestrictedClient($"Finance.Accounts.Settings.{setting}.Add");
		var response = await client.PostAsJsonAsync($"/api/{route}{query}", new { name = "Missing academic year" });
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Retired_account_head_resource_returns_gone()
	{
		using var client = RestrictedClient("Finance.Accounts.Settings.IncomeCategory.View");
		var response = await client.GetAsync($"/api/account-heads/options?academicYearId={Factory.SeededAcademicYearId}&headType=Income");
		response.StatusCode.ShouldBe(HttpStatusCode.Gone);
	}

	private HttpClient RestrictedClient(string permission)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "accountant");
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permission);
		return client;
	}
}
