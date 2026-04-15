using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
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
	private const string DeductionBaseUrl = "/api/payroll/deduction-components";

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

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Field_Level_Validation_Errors_For_Invalid_Enum_Input()
	{
		var input = new EarningComponentCreateRequestDto
		{
			EarningComponentName = TestDataHelper.UniqueName("InvalidType"),
			EarningType = "UNKNOWN_TYPE",
			CalculationType = "AMOUNT",
			Amount = 2500m,
			VisibleInPayslip = true
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);

		if (response.StatusCode != HttpStatusCode.BadRequest)
		{
			return;
		}

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;

		var error = root.GetProperty("error");
		error.GetProperty("validationErrors").ValueKind.ShouldBe(JsonValueKind.Array);

		var validationErrors = error.GetProperty("validationErrors").EnumerateArray().ToList();
		var keys = validationErrors
			.SelectMany(ve => ve.GetProperty("members").EnumerateArray())
			.Select(m => m.GetString())
			.Where(m => !string.IsNullOrWhiteSpace(m))
			.Select(m => m!)
			.ToList();
		keys.Any(k => k.Contains("earningType", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Custom_Component_Name_In_List_Response()
	{
		var baseComponent = await CreateComponentAsync("BaseForNestedName");
		var dependentName = TestDataHelper.UniqueName("DependsOnBase");

		var createDependent = new EarningComponentCreateRequestDto
		{
			EarningComponentName = dependentName,
			EarningType = "FIXED",
			CalculationType = "PERCENTAGE",
			Percentage = 10m,
			SelectedEarningComponentId = baseComponent.Id,
			VisibleInPayslip = true
		};

		var createResponse = await Client.PostAsJsonAsync(BaseUrl, createDependent, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

		var listResponse = await Client.GetAsync($"{BaseUrl}?search={dependentName}");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var body = await listResponse.Content.ReadFromJsonAsync<EarningComponentPaginatedResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		var dependent = body.Items.FirstOrDefault(x => x.EarningComponentName == dependentName);
		dependent.ShouldNotBeNull();
		dependent.CustomComponent.ShouldNotBeNull();
		dependent.CustomComponent!.ComponentName.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Allow_Recreate_Same_Name_After_Delete()
	{
		var name = TestDataHelper.UniqueName("RecreateAfterDelete");
		var input = new EarningComponentCreateRequestDto
		{
			EarningComponentName = name,
			EarningType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = 5000m,
			VisibleInPayslip = true
		};

		var firstCreate = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		firstCreate.StatusCode.ShouldBe(HttpStatusCode.Created);
		var firstBody = await firstCreate.Content.ReadFromJsonAsync<ComponentCreateResponseDto>(JsonOptions);
		firstBody.ShouldNotBeNull();

		var deleteResponse = await Client.DeleteAsync($"{BaseUrl}/{firstBody.Id}");
		deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var secondCreate = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		secondCreate.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Duplicate_When_Name_Is_Logically_Same_After_Trim()
	{
		var baseName = TestDataHelper.UniqueName("PadDup");
		var firstInput = new EarningComponentCreateRequestDto
		{
			EarningComponentName = baseName,
			EarningType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = 1000m,
			VisibleInPayslip = true
		};

		var first = await Client.PostAsJsonAsync(BaseUrl, firstInput, JsonOptions);
		first.StatusCode.ShouldBe(HttpStatusCode.Created);

		var paddedDuplicate = new EarningComponentCreateRequestDto
		{
			EarningComponentName = $"  {baseName}  ",
			EarningType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = 2000m,
			VisibleInPayslip = true
		};

		var second = await Client.PostAsJsonAsync(BaseUrl, paddedDuplicate, JsonOptions);
		second.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Allow_Earning_Name_That_Matches_Deduction_Catalog_Name()
	{
		var shared = TestDataHelper.UniqueName("SharedAcrossTypes");
		var deductionInput = new DeductionComponentCreateRequestDto
		{
			DeductionComponentName = shared,
			DeductionType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = 500m,
			VisibleInPayslip = true
		};
		var deductionCreate = await Client.PostAsJsonAsync(DeductionBaseUrl, deductionInput, JsonOptions);
		deductionCreate.StatusCode.ShouldBe(HttpStatusCode.Created);

		var baseForPct = await CreateComponentAsync("BaseForCrossTypePct");
		var earningInput = new EarningComponentCreateRequestDto
		{
			EarningComponentName = shared,
			EarningType = "FIXED",
			CalculationType = "PERCENTAGE",
			Percentage = 15m,
			SelectedEarningComponentId = baseForPct.Id,
			VisibleInPayslip = false
		};

		var earningCreate = await Client.PostAsJsonAsync(BaseUrl, earningInput, JsonOptions);
		earningCreate.StatusCode.ShouldBe(HttpStatusCode.Created);
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
