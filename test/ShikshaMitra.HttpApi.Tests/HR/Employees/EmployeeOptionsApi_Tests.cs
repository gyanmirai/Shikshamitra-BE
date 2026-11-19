using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees.Search;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class EmployeeOptionsApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Options_Should_Page_Without_Duplicates_And_Return_Only_Picker_Fields()
	{
		var prefix = $"Options{Guid.NewGuid():N}";
		var first = await EmployeeTestHelper.CreateEmployeeAsync(Client, prefix);
		var second = await EmployeeTestHelper.CreateEmployeeAsync(Client, prefix);
		var url = $"/api/employees/options?search={prefix}&pageSize=1";
		var page1 = await Client.GetFromJsonAsync<EmployeeOptionsResponse>($"{url}&page=1", JsonOptions);
		var page2 = await Client.GetFromJsonAsync<EmployeeOptionsResponse>($"{url}&page=2", JsonOptions);
		var page3 = await Client.GetFromJsonAsync<EmployeeOptionsResponse>($"{url}&page=3", JsonOptions);
		page1!.Items.Count.ShouldBe(1);
		page1.HasMore.ShouldBeTrue();
		page2!.Items.Count.ShouldBe(1);
		page2.HasMore.ShouldBeFalse();
		page3!.Items.ShouldBeEmpty();
		page3.HasMore.ShouldBeFalse();
		page1.Items.Concat(page2.Items).Select(item => item.Id).OrderBy(id => id)
			.ShouldBe(new[] { first.Id, second.Id }.OrderBy(id => id));
		var payload = await Client.GetStringAsync(url);
		payload.ShouldNotContain("email");
		payload.ShouldNotContain("organizations");
		payload.ShouldNotContain("shift");
		payload.ShouldNotContain("totalCount");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Options_Should_Keep_Full_Name_Search_And_Active_Filter()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "OptionName");
		await EmployeeTestHelper.SetActiveStatusForTestAsync(Factory, created.Id);
		var query = Uri.EscapeDataString($"{created.LastName}\t{created.FirstName}");
		var found = await Client.GetFromJsonAsync<EmployeeOptionsResponse>(
			$"/api/employees/options?search={query}", JsonOptions);
		found!.Items.Select(item => item.Id).ShouldBe(new[] { created.Id });
		found.Items[0].FullName.ShouldBe($"{created.FirstName} {created.LastName}");
		var future = await EmployeeTestHelper.CreateEmployeeAsync(Client, "OptionFuture", dateOfJoining: DateTime.UtcNow.AddDays(5));
		await EmployeeTestHelper.SetActiveStatusForTestAsync(Factory, future.Id);
		var currentActive = await Client.GetFromJsonAsync<EmployeeOptionsResponse>(
			$"/api/employees/options?search={Uri.EscapeDataString(created.FirstName)}&status=Active", JsonOptions);
		currentActive!.Items.Select(item => item.Id).ShouldBe(new[] { created.Id });
		var active = await Client.GetFromJsonAsync<EmployeeOptionsResponse>(
			$"/api/employees/options?search={Uri.EscapeDataString(future.FirstName)}&status=Active", JsonOptions);
		active!.Items.ShouldBeEmpty();
		var inactive = await EmployeeTestHelper.CreateEmployeeAsync(Client, "OptionInactive");
		var excludedInactive = await Client.GetFromJsonAsync<EmployeeOptionsResponse>(
			$"/api/employees/options?search={Uri.EscapeDataString(inactive.FirstName)}&status=Active", JsonOptions);
		excludedInactive!.Items.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Options_Should_Require_Exact_Employee_List_View_Permission()
	{
		using var denied = new HttpRequestMessage(HttpMethod.Get, "/api/employees/options");
		denied.Headers.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit);
		using var response = await Client.SendAsync(denied);
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Options_Should_Reject_Unknown_Tenant()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "OptionTenant");
		using var request = new HttpRequestMessage(HttpMethod.Get,
			$"/api/employees/options?search={Uri.EscapeDataString(created.FirstName)}");
		request.Headers.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		using var response = await Client.SendAsync(request);
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await response.Content.ReadAsStringAsync()).ShouldNotContain(created.Id.ToString());
	}

	[Theory]
	[InlineData("page=0")]
	[InlineData("pageSize=51")]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Options_Should_Reject_Unbounded_Requests(string query)
	{
		using var response = await Client.GetAsync($"/api/employees/options?{query}");
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}
}
