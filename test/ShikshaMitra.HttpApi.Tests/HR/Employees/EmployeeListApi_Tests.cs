using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Status;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class EmployeeListApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/employees";

	public EmployeeListApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Employee_List_Returns_200()
	{
		await EmployeeTestHelper.CreateEmployeeAsync(Client, "ListTest");

		var response = await Client.GetAsync($"{BaseUrl}?page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EmployeeListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
		body.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Employee_By_Id()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "GetById");

		var response = await Client.GetAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EmployeeDetails>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldBe(created.Id);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_404_For_Nonexistent_Employee()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.NotFound,
			HttpStatusCode.InternalServerError,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Filter_By_Status()
	{
		await EmployeeTestHelper.CreateEmployeeAsync(Client, "StatusFilter");

		var response = await Client.GetAsync($"{BaseUrl}?status=Active&page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EmployeeListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Exclude_Inactive_Employees_When_Filtering_Active_Status()
	{
		var marker = $"ActiveOnly{Guid.NewGuid():N}"[..18];
		var activeEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"{marker}Active");
		var inactiveEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"{marker}Inactive");

		var inactiveRequest = new EmployeeStatusUpdateRequest
		{
			EmployeeStatusType = 1,
			Remarks = "Inactive employee should be excluded from active picker queries",
			EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow)
		};
		(await Client.PutAsJsonAsync($"{BaseUrl}/{inactiveEmployee.Id}/status", inactiveRequest, JsonOptions))
			.EnsureSuccessStatusCode();

		var response = await Client.GetAsync(
			$"{BaseUrl}?status=Active&search={Uri.EscapeDataString(marker)}&page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EmployeeListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldContain(e => e.Id == activeEmployee.Id);
		body.Items.ShouldNotContain(e => e.Id == inactiveEmployee.Id);
		body.Items.ShouldAllBe(e => e.Status == 0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Filter_By_IsTeaching()
	{
		await EmployeeTestHelper.CreateEmployeeAsync(Client, "TeachFilter");

		var response = await Client.GetAsync($"{BaseUrl}?isTeaching=true&page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EmployeeListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
		body.Items.ShouldAllBe(e => e.IsTeaching);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Filter_By_NonTeaching()
	{
		await EmployeeTestHelper.CreateEmployeeAsync(Client, "NonTeachFilter", isTeaching: false);

		var response = await Client.GetAsync($"{BaseUrl}?isTeaching=false&page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EmployeeListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
		body.Items.ShouldAllBe(e => !e.IsTeaching);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Filter_Teaching_And_NonTeaching_With_Search()
	{
		var marker = $"Group{Guid.NewGuid():N}"[..17];
		var teachingEmployee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"{marker}Teach",
			isTeaching: true);
		var nonTeachingEmployee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"{marker}NonTeach",
			isTeaching: false);

		var teachingResponse = await Client.GetAsync(
			$"{BaseUrl}?isTeaching=true&search={Uri.EscapeDataString(marker)}&page=1&pageSize=10");
		var nonTeachingResponse = await Client.GetAsync(
			$"{BaseUrl}?isTeaching=false&search={Uri.EscapeDataString(marker)}&page=1&pageSize=10");

		teachingResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		nonTeachingResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var teachingBody = await teachingResponse.Content.ReadFromJsonAsync<EmployeeListResponse>(JsonOptions);
		var nonTeachingBody = await nonTeachingResponse.Content.ReadFromJsonAsync<EmployeeListResponse>(JsonOptions);

		teachingBody.ShouldNotBeNull();
		nonTeachingBody.ShouldNotBeNull();
		teachingBody.Items.ShouldContain(e => e.Id == teachingEmployee.Id);
		teachingBody.Items.ShouldNotContain(e => e.Id == nonTeachingEmployee.Id);
		nonTeachingBody.Items.ShouldContain(e => e.Id == nonTeachingEmployee.Id);
		nonTeachingBody.Items.ShouldNotContain(e => e.Id == teachingEmployee.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Filter_By_RoleId()
	{
		var randomRoleId = Guid.NewGuid();

		var response = await Client.GetAsync($"{BaseUrl}?roleId={randomRoleId}&page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EmployeeListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Search_By_Employee_Name()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "SearchUnique");

		var searchTerm = created.FirstName;
		var response = await Client.GetAsync($"{BaseUrl}?search={searchTerm}&page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EmployeeListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
	}

	#endregion

	#region Tier 2 — Extended

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Paginate_Employee_List()
	{
		for (var i = 0; i < 15; i++)
			await EmployeeTestHelper.CreateEmployeeAsync(Client, $"Page_{i}");

		var page1 = await Client.GetAsync($"{BaseUrl}?page=1&pageSize=10");
		page1.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body1 = await page1.Content.ReadFromJsonAsync<EmployeeListResponse>(JsonOptions);
		body1.ShouldNotBeNull();
		body1.Items.Count.ShouldBeLessThanOrEqualTo(10);

		var page2 = await Client.GetAsync($"{BaseUrl}?page=2&pageSize=10");
		page2.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body2 = await page2.Content.ReadFromJsonAsync<EmployeeListResponse>(JsonOptions);
		body2.ShouldNotBeNull();
		body2.Items.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_All_Employees_When_PageSize_Matches_TotalCount()
	{
		var marker = $"AllPage{Guid.NewGuid():N}"[..15];
		const int employeeCount = 21;

		for (var i = 0; i < employeeCount; i++)
			await EmployeeTestHelper.CreateEmployeeAsync(Client, $"{marker}{i}");

		var response = await Client.GetAsync(
			$"{BaseUrl}?search={Uri.EscapeDataString(marker)}&page=1&pageSize={employeeCount}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EmployeeListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.PageSize.ShouldBe(employeeCount);
		body.TotalCount.ShouldBeGreaterThanOrEqualTo(employeeCount);
		body.Items.Count.ShouldBe(employeeCount);
		body.Items.ShouldAllBe(e => e.FullName != null && e.FullName.Contains(marker));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Verify_Response_Contract_Fields()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "ContractVerify");

		var response = await Client.GetAsync($"{BaseUrl}/{created.Id}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EmployeeDetails>(JsonOptions);

		body.ShouldNotBeNull();
		body.Id.ShouldNotBe(Guid.Empty);
		body.FullName.ShouldNotBeNullOrWhiteSpace();
		body.EmployeeIdentifier.ShouldNotBeNullOrWhiteSpace();
		body.MobileNo.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion
}
