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

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class AccountSettingsPermissionApi_Tests(ShikshaMitraApiTestFactory factory)
	: ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData("account-heads", "AccountHead")]
	[InlineData("income-categories", "IncomeCategory")]
	[InlineData("expense-categories", "ExpenseCategory")]
	[InlineData("bank-accounts", "BankAccount")]
	public async Task Settings_Module_Grant_Should_Not_Grant_View(string route, string setting)
	{
		using var client = RestrictedClient("ShikshaMitra.Module.Finance.Accounts.Settings");
		var response = await client.GetAsync($"/api/{route}?academicYearId={Factory.SeededAcademicYearId}");
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Theory]
	[InlineData("account-heads", "AccountHead")]
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

	[Fact]
	public async Task Income_Category_View_Should_Not_Grant_Account_Head_Lookup()
	{
		using var client = RestrictedClient("Finance.Accounts.Settings.IncomeCategory.View");
		var response = await client.GetAsync($"/api/account-heads/options?academicYearId={Factory.SeededAcademicYearId}&headType=Income");
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	private HttpClient RestrictedClient(string permission)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "accountant");
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permission);
		return client;
	}
}
