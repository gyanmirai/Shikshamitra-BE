using System;
using System.Net;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using ShikshaMitra.Finance.Accounts.BankActivity;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;
namespace ShikshaMitra.Finance.Accounts;
[Collection(FinanceRoutingApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class BankAccountActivityApi_Tests(FinanceRoutingApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData("Finance.Accounts.IncomeTracking.View")]
	[InlineData("Finance.Accounts.Settings.BankAccount.Add")]
	[InlineData("ShikshaMitra.Module.Finance.Accounts")]
	public async Task Exact_bank_view_is_required_for_all_four_endpoints(string permission)
	{
		using var client = Restricted(permission); var prefix = $"/api/bank-accounts/{Guid.NewGuid()}/"; var query = $"?academicYearId={Factory.SeededAcademicYearId}";
		(await client.GetAsync(prefix + "activity" + query)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.GetAsync(prefix + "deposits" + query)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.GetAsync(prefix + "activity/transaction:any" + query)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.PostAsJsonAsync(prefix + "activity/export", new { query = new { academicYearId = Factory.SeededAcademicYearId }, format = "excel", columns = Array.Empty<object>() })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Fact]
	public async Task Bank_settings_view_alone_never_grants_operational_activity_or_export()
	{
		var response = await Client.PostAsJsonAsync($"/api/bank-accounts?academicYearId={Factory.SeededAcademicYearId}", new { bankName = "Bank activity permission test", branchName = "Branch", accountHolderName = "School", accountType = 0, accountNumber = DateTime.UtcNow.Ticks.ToString()[^14..], ifscCode = "TEST0000123" }, JsonOptions);
		response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync()); var bank = (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();
		using var client = Restricted("Finance.Accounts.Settings.BankAccount.View"); var prefix = $"/api/bank-accounts/{bank}/"; var query = $"?academicYearId={Factory.SeededAcademicYearId}";
		(await client.GetAsync(prefix + "activity" + query)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.GetAsync(prefix + "deposits" + query)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.GetAsync(prefix + "activity/transaction:any" + query)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.PostAsJsonAsync(prefix + "activity/export", new { query = new { academicYearId = Factory.SeededAcademicYearId }, format = "excel", columns = Array.Empty<object>() })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Fact]
	public void Export_workbook_preserves_all_matching_rows_and_paise_beyond_the_loaded_page()
	{
		var rows = Enumerable.Range(1, 27).Select(x => new BankActivityRow { Reference = $"Movement {x}", Incoming = x + .01m, OccurredAt = DateTimeOffset.UtcNow }).ToList();
		var page = new BankActivityPage { AcademicYearName = "2026", Items = rows.Take(10).ToList(), TotalCount = 27, Summary = new() { Incoming = 378.27m, Net = 378.27m }, Account = new() { BankName = "School bank", AccountNumber = "123456789012", MaskedAccountNumber = "********9012" } };
		var columns = new List<BankActivityExportColumn> { new("reference", "Reference"), new("incoming", "Incoming (INR)") };
		var method = typeof(BankAccountActivityAppService).GetMethod("Excel", BindingFlags.Static | BindingFlags.NonPublic)!;
		var bytes = (byte[]) method.Invoke(null, [page, rows, columns])!;
		using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(bytes));
		var activity = workbook.Worksheet("Activity");
		activity.RowsUsed().Count().ShouldBe(28);
		Enumerable.Range(2, 27).Sum(row => activity.Cell(row, 2).GetValue<decimal>()).ShouldBe(378.27m);
		var context = workbook.Worksheet("Context");
		context.RowsUsed().Single(row => row.Cell(1).GetString() == "Incoming").Cell(2).GetValue<decimal>().ShouldBe(378.27m);
		context.Cell(1, 2).GetString().ShouldContain("********9012");
		context.Cell(1, 2).GetString().ShouldNotContain("123456789012");
	}
	[Fact]
	public void Export_pdf_is_a_real_document_from_the_same_scoped_rows()
	{
		var page = new BankActivityPage { AcademicYearName = "2026", Summary = new() { Incoming = 12.34m, Net = 12.34m }, Account = new() { BankName = "School bank", MaskedAccountNumber = "********9012" } };
		var rows = new List<BankActivityRow> { new() { Reference = "Movement", Incoming = 12.34m, OccurredAt = DateTimeOffset.UtcNow } };
		var columns = new List<BankActivityExportColumn> { new("reference", "Reference"), new("incoming", "Incoming (INR)") };
		var method = typeof(BankAccountActivityAppService).GetMethod("Pdf", BindingFlags.Static | BindingFlags.NonPublic)!;
		var bytes = (byte[]) method.Invoke(null, [page, rows, columns])!;
		System.Text.Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
		bytes.Length.ShouldBeGreaterThan(1000);
	}
	private HttpClient Restricted(string permission)
	{ var client = Factory.CreateClient(); client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "accountant"); client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permission); return client; }
}
