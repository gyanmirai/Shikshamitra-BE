using System;
using System.Net;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Mobile;

[Collection(FinanceRoutingApiTestCollection.Name)]
[Trait("Module","Finance")]
[Trait("Tier","Critical")]
public class MobileFinancePermissionApi_Tests(FinanceRoutingApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData("context")]
	[InlineData("overview")]
	[InlineData("installments")]
	[InlineData("charges")]
	[InlineData("charges/page")]
	[InlineData("charges/99999999-9999-9999-9999-999999999999")]
	[InlineData("charges/99999999-9999-9999-9999-999999999999/payments")]
	[InlineData("payments")]
	[InlineData("receipts")]
	[InlineData("requests")]
	[InlineData("activity")]
	[InlineData("notifications")]
	[InlineData("statement/pdf")]
	public async Task Family_routes_reject_anonymous_requests(string suffix)
	{
		using var client=CreateAnonymousClient();
		client.DefaultRequestHeaders.Add("__tenant",TestAuthHandler.TestTenantId);
		using var response=await client.GetAsync(Url(suffix));
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized,await response.Content.ReadAsStringAsync());
	}
	[Theory]
	[InlineData("context")]
	[InlineData("overview")]
	[InlineData("installments")]
	[InlineData("charges")]
	[InlineData("charges/page")]
	[InlineData("charges/99999999-9999-9999-9999-999999999999")]
	[InlineData("charges/99999999-9999-9999-9999-999999999999/payments")]
	[InlineData("payments")]
	[InlineData("receipts")]
	[InlineData("requests")]
	[InlineData("activity")]
	[InlineData("notifications")]
	[InlineData("statement/pdf")]
	public async Task Existing_finance_grant_still_requires_persisted_family_association(string suffix)
	{
		using var client=Factory.CreateClient();
		client.DefaultRequestHeaders.Add("__tenant",TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader,"accountant");
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			suffix.StartsWith("charges", StringComparison.Ordinal) ? "Finance.Fees.Charges.View" : "Finance.Fees.FeeCollections.View");
		using var response=await client.GetAsync(Url(suffix));
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden,await response.Content.ReadAsStringAsync());
	}
	[Fact]
	public async Task Old_self_service_grant_alone_does_not_grant_collection_read()
	{
		using var client=Factory.CreateClient();
		client.DefaultRequestHeaders.Add("__tenant",TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,"Finance.SelfService.View");
		using var response=await client.GetAsync(Url("overview"));
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden,await response.Content.ReadAsStringAsync());
	}
	private string Url(string suffix)=>$"/api/mobile/v1/finance/students/{Guid.NewGuid()}/{suffix}?academicYearId={Factory.SeededAcademicYearId}";
}
