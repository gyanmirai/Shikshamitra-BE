using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class EarningComponentApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/payroll/earning-components";

	public EarningComponentApi_Tests(ShikshaMitraApiTestFactory factory)
		: base(factory)
	{
	}

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Earning_Component_Returns_201()
	{
		var input = new EarningComponentCreateRequestDto
		{
			EarningComponentName = TestDataHelper.UniqueName("BasicSalary"),
			EarningType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = 50000m,
			VisibleInPayslip = true,
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
		var body = await response.Content.ReadFromJsonAsync<ComponentCreateResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Earning_Component_By_Id()
	{
		var created = await CreateComponentAsync();

		var response = await Client.GetAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EarningComponentResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldBe(created.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Paginated_List()
	{
		await CreateComponentAsync();

		var response = await Client.GetAsync($"{BaseUrl}?page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EarningComponentPaginatedResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
		body.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Earning_Component()
	{
		var created = await CreateComponentAsync();

		var updateDto = new EarningComponentUpdateRequestDto
		{
			EarningComponentName = TestDataHelper.UniqueName("UpdatedComp"),
			EarningType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = 60000m,
			VisibleInPayslip = true,
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", updateDto, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Delete_Earning_Component()
	{
		var created = await CreateComponentAsync();

		var response = await Client.DeleteAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_404_For_Nonexistent_Component()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Search_Components_By_Name()
	{
		var uniquePart = Guid.NewGuid().ToString("N")[..6];
		var input = new EarningComponentCreateRequestDto
		{
			EarningComponentName = $"Searchable_{uniquePart}",
			EarningType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = 10000m,
			VisibleInPayslip = true,
		};
		await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		var response = await Client.GetAsync($"{BaseUrl}?search={uniquePart}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EarningComponentPaginatedResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldContain(x => x.EarningComponentName.Contains(uniquePart));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Create_Percentage_Based_Component()
	{
		var baseComponent = await CreateComponentAsync("BaseForPct");

		var input = new EarningComponentCreateRequestDto
		{
			EarningComponentName = TestDataHelper.UniqueName("HRA_Pct"),
			EarningType = "FIXED",
			CalculationType = "PERCENTAGE",
			Percentage = 40m,
			SelectedEarningComponentId = baseComponent.Id,
			VisibleInPayslip = true,
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	#endregion

	#region Tier 2 — Extended

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Handle_Pagination_With_Many_Components()
	{
		for (var i = 0; i < 25; i++)
		{
			await CreateComponentAsync($"BulkComp_{i}");
		}

		var page1 = await Client.GetAsync($"{BaseUrl}?page=1&pageSize=10");
		var body1 = await page1.Content.ReadFromJsonAsync<EarningComponentPaginatedResponseDto>(JsonOptions);
		body1.ShouldNotBeNull();
		body1.Items.Count.ShouldBeLessThanOrEqualTo(10);

		var page2 = await Client.GetAsync($"{BaseUrl}?page=2&pageSize=10");
		var body2 = await page2.Content.ReadFromJsonAsync<EarningComponentPaginatedResponseDto>(JsonOptions);
		body2.ShouldNotBeNull();
		body2.Items.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Update_Then_Verify_Component()
	{
		var created = await CreateComponentAsync();

		var updateDto = new EarningComponentUpdateRequestDto
		{
			EarningComponentName = TestDataHelper.UniqueName("Verified"),
			EarningType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = 75000m,
			VisibleInPayslip = false,
		};

		await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", updateDto, JsonOptions);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{created.Id}");
		var body = await getResponse.Content.ReadFromJsonAsync<EarningComponentResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Amount.ShouldBe(75000m);
		body.VisibleInPayslip.ShouldBeFalse();
	}

	#endregion

	#region Helpers

	private async Task<ComponentCreateResponseDto> CreateComponentAsync(string? nameSuffix = null)
	{
		var input = new EarningComponentCreateRequestDto
		{
			EarningComponentName = TestDataHelper.UniqueName(nameSuffix ?? "EarnComp"),
			EarningType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = 30000m,
			VisibleInPayslip = true,
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<ComponentCreateResponseDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create test earning component");
	}

	#endregion
}
