using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

[Collection(LibraryApiTestCollection.Name)]
public partial class LibraryDashboardFinanceApi_Tests(LibraryApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Fact]
	public async Task Dashboard_Finance_Http_Should_Return_An_Empty_Position_With_Only_Its_Own_Read_Action()
	{
		var context = await SeedDashboardContextAsync(assignScope: true);
		using var client = DashboardClient(context.Actor, ShikshaMitraSubmodulePermissions.Library.Dashboard.View);
		using var response = await client.GetAsync($"/api/library/dashboard/overview?academicYearId={context.Year:D}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		var summary = document.RootElement.GetProperty("financePosition");
		summary.GetProperty("verifiedCount").GetInt32().ShouldBe(0);
		summary.GetProperty("needsReviewCount").GetInt32().ShouldBe(0);
		summary.GetProperty("unpostedCount").GetInt32().ShouldBe(0);
		summary.GetProperty("currencyTotals").GetArrayLength().ShouldBe(0);
	}

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.Charges.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
	public async Task Dashboard_Finance_Http_Should_Deny_Unrelated_Read_Actions(string unrelatedAction)
	{
		var context = await SeedDashboardContextAsync(assignScope: true);
		using var client = DashboardClient(context.Actor, unrelatedAction);
		using var response = await client.GetAsync($"/api/library/dashboard/overview?academicYearId={context.Year:D}");
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Dashboard_Finance_Http_Should_Deny_A_Reader_Without_Any_Persisted_Scope()
	{
		var context = await SeedDashboardContextAsync(assignScope: false);
		using var client = DashboardClient(context.Actor, ShikshaMitraSubmodulePermissions.Library.Dashboard.View);
		using var response = await client.GetAsync($"/api/library/dashboard/overview?academicYearId={context.Year:D}");
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		document.RootElement.GetProperty("errorCode").GetString().ShouldBe(UserScopeErrorCodes.AccessDenied);
		document.RootElement.TryGetProperty("financePosition", out _).ShouldBeFalse();
	}
}
