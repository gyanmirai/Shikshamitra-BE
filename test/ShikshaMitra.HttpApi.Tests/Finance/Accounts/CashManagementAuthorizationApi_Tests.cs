using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

[Collection(FinanceRoutingApiTestCollection.Name)]
public class CashManagementAuthorizationApi_Tests(FinanceRoutingApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData("Finance.Accounts.IncomeTracking.View")]
	[InlineData("Finance.Accounts.CashManagement.Approve")]
	[InlineData("Finance.Accounts.CashManagement.Deposit")]
	[InlineData("Finance.Accounts.CashManagement.Reconcile")]
	public async Task Cash_read_requires_its_exact_View_leaf(string permission)
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permission);
		(await client.GetAsync($"/api/finance/cash-management?academicYearId={Factory.SeededAcademicYearId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.GetAsync($"/api/finance/cash-management/{Guid.NewGuid()}?academicYearId={Factory.SeededAcademicYearId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Theory]
	[InlineData("approve", "Finance.Accounts.CashManagement.View")]
	[InlineData("deposit", "Finance.Accounts.CashManagement.Approve")]
	[InlineData("expense", "Finance.Accounts.CashManagement.Deposit")]
	public async Task Source_and_bank_options_require_the_owning_action(string operation, string permission)
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permission);
		(await client.GetAsync($"/api/finance/cash-management/options?academicYearId={Factory.SeededAcademicYearId}&operation={operation}"))
			.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Theory]
	[InlineData("approve")]
	[InlineData("deposits")]
	[InlineData("confirm")]
	public async Task View_cannot_write_cash_or_credit(string action)
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "Finance.Accounts.CashManagement.View");
		var id = Guid.NewGuid();
		var path = action == "confirm" ? $"{id}/deposits/{Guid.NewGuid()}/confirm" : $"{id}/{action}";
		var response = await client.PostAsJsonAsync($"/api/finance/cash-management/{path}", new
		{
			academicYearId = Factory.SeededAcademicYearId, idempotencyKey = Guid.NewGuid(), concurrencyStamp = "test",
			bankAccountId = Guid.NewGuid(), countedAmount = 1m, amount = 1m, reference = "test",
			depositDate = DateTimeOffset.UtcNow, creditDate = DateTimeOffset.UtcNow
		});
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}
}
