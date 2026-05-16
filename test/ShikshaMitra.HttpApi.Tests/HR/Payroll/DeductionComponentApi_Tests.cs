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
public class DeductionComponentApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/payroll/deduction-components";
	private const string EarningComponentsBaseUrl = "/api/payroll/earning-components";

	public DeductionComponentApi_Tests(ShikshaMitraApiTestFactory factory)
		: base(factory)
	{
	}

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Deduction_Component_Returns_201()
	{
		var input = new DeductionComponentCreateRequestDto
		{
			DeductionComponentName = TestDataHelper.UniqueName("PF"),
			DeductionType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = 1800m,
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
	public async Task Should_Get_Deduction_Component_By_Id()
	{
		var created = await CreateComponentAsync();

		var response = await Client.GetAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<DeductionComponentResponseDto>(JsonOptions);
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
		var body = await response.Content.ReadFromJsonAsync<DeductionComponentPaginatedResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
		body.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Deduction_Component()
	{
		var created = await CreateComponentAsync();

		var updateDto = new DeductionComponentUpdateRequestDto
		{
			DeductionComponentName = TestDataHelper.UniqueName("UpdatedDeduction"),
			DeductionType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = 5000m,
			VisibleInPayslip = true,
		};

		var response = await PutWithPreviewAsync(created.Id, updateDto);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Delete_Deduction_Component()
	{
		var created = await CreateComponentAsync();

		var response = await DeleteWithPreviewAsync(created.Id);

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
		var input = new DeductionComponentCreateRequestDto
		{
			DeductionComponentName = $"Searchable_{uniquePart}",
			DeductionType = "VARIABLE",
			CalculationType = "AMOUNT",
			Amount = 2000m,
			VisibleInPayslip = true,
		};
		await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		var response = await Client.GetAsync($"{BaseUrl}?search={uniquePart}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<DeductionComponentPaginatedResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldContain(x => x.DeductionComponentName.Contains(uniquePart));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Create_Flat_Amount_Deduction()
	{
		var input = new DeductionComponentCreateRequestDto
		{
			DeductionComponentName = TestDataHelper.UniqueName("LoanEMI"),
			DeductionType = "VARIABLE",
			CalculationType = "AMOUNT",
			Amount = 5000m,
			VisibleInPayslip = true,
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Create_Variable_Amount_Deduction_Without_Amount()
	{
		var input = new DeductionComponentCreateRequestDto
		{
			DeductionComponentName = TestDataHelper.UniqueName("VariableNoDefault"),
			DeductionType = "VARIABLE",
			CalculationType = "AMOUNT",
			Amount = null,
			VisibleInPayslip = true,
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Create_Percentage_Deduction_Without_Amount()
	{
		var baseComponent = await CreateComponentAsync("BaseForPercentNoAmount");
		var input = new DeductionComponentCreateRequestDto
		{
			DeductionComponentName = TestDataHelper.UniqueName("PercentNoAmount"),
			DeductionType = "FIXED",
			CalculationType = "PERCENTAGE",
			Amount = null,
			Percentage = 12m,
			SelectedDeductionComponentId = baseComponent.Id,
			VisibleInPayslip = true,
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Fixed_Amount_Deduction_Without_Amount()
	{
		var input = new DeductionComponentCreateRequestDto
		{
			DeductionComponentName = TestDataHelper.UniqueName("FixedNoAmount"),
			DeductionType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = null,
			VisibleInPayslip = true,
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Update_To_Variable_Amount_Deduction_Without_Amount()
	{
		var created = await CreateComponentAsync("UpdateVariableNoAmount");
		var updateDto = new DeductionComponentUpdateRequestDto
		{
			DeductionComponentName = TestDataHelper.UniqueName("UpdatedVariableNoAmount"),
			DeductionType = "VARIABLE",
			CalculationType = "AMOUNT",
			Amount = null,
			VisibleInPayslip = true,
		};

		var response = await PutWithPreviewAsync(created.Id, updateDto);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Field_Level_Validation_Errors_For_Invalid_Enum_Input()
	{
		var input = new DeductionComponentCreateRequestDto
		{
			DeductionComponentName = TestDataHelper.UniqueName("InvalidType"),
			DeductionType = "UNKNOWN_TYPE",
			CalculationType = "AMOUNT",
			Amount = 1500m,
			VisibleInPayslip = true
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
		response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;

		root.TryGetProperty("status", out var statusElement).ShouldBeTrue();
		statusElement.GetInt32().ShouldBe((int)response.StatusCode);
		root.TryGetProperty("title", out var titleElement).ShouldBeTrue();
		titleElement.GetString().ShouldNotBeNullOrWhiteSpace();
		root.TryGetProperty("type", out _).ShouldBeTrue();
		root.TryGetProperty("detail", out _).ShouldBeTrue();
		root.TryGetProperty("instance", out _).ShouldBeTrue();

		if (response.StatusCode == HttpStatusCode.BadRequest)
		{
			root.TryGetProperty("errors", out var errors).ShouldBeTrue();
			errors.ValueKind.ShouldBe(JsonValueKind.Object);
			var keys = errors.EnumerateObject()
				.Select(e => e.Name)
				.ToList();
			keys.Any(k => k.Contains("deductionType", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Deduction_Base_Component_Name_In_List_Response()
	{
		var baseComponent = await CreateComponentWithNameAsync("BaseForNestedName");
		var dependentName = TestDataHelper.UniqueName("DependsOnBase");

		var createDependent = new DeductionComponentCreateRequestDto
		{
			DeductionComponentName = dependentName,
			DeductionType = "FIXED",
			CalculationType = "PERCENTAGE",
			Percentage = 8m,
			SelectedDeductionComponentId = baseComponent.Created.Id,
			VisibleInPayslip = true
		};

		var createResponse = await Client.PostAsJsonAsync(BaseUrl, createDependent, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

		var listResponse = await Client.GetAsync($"{BaseUrl}?search={dependentName}");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var body = await listResponse.Content.ReadFromJsonAsync<DeductionComponentPaginatedResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		var dependent = body.Items.FirstOrDefault(x => x.DeductionComponentName == dependentName);
		dependent.ShouldNotBeNull();
		dependent.CustomComponent.ShouldNotBeNull();
		dependent.CustomComponent!.ComponentName.ShouldBe(baseComponent.Name);
		dependent.CustomComponent.ComponentOf.ShouldBe("DEDUCTION_COMPONENT");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Earning_Base_Component_Name_In_List_Response()
	{
		var earningBase = await CreateEarningComponentWithNameAsync("BasicSalaryForDeduction");
		var dependentName = TestDataHelper.UniqueName("PFDependsOnBasic");

		var createDependent = new DeductionComponentCreateRequestDto
		{
			DeductionComponentName = dependentName,
			DeductionType = "FIXED",
			CalculationType = "PERCENTAGE",
			Percentage = 12m,
			SelectedDeductionComponentId = earningBase.Created.Id,
			VisibleInPayslip = true
		};

		var createResponse = await Client.PostAsJsonAsync(BaseUrl, createDependent, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

		var listResponse = await Client.GetAsync($"{BaseUrl}?search={dependentName}");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var body = await listResponse.Content.ReadFromJsonAsync<DeductionComponentPaginatedResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		var dependent = body.Items.FirstOrDefault(x => x.DeductionComponentName == dependentName);
		dependent.ShouldNotBeNull();
		dependent.CustomComponent.ShouldNotBeNull();
		dependent.CustomComponent!.ComponentName.ShouldBe(earningBase.Name);
		dependent.CustomComponent.ComponentOf.ShouldBe("EARNING_COMPONENT");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Null_Custom_Component_For_Amount_Deduction()
	{
		var created = await CreateComponentAsync("AmountWithoutCustomComponent");

		var response = await Client.GetAsync($"{BaseUrl}/{created.Id}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var body = await response.Content.ReadFromJsonAsync<DeductionComponentResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.CustomComponent.ShouldBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Null_Custom_Component_For_Unresolved_Percentage_Base()
	{
		var dependentName = TestDataHelper.UniqueName("MissingBase");
		var createDependent = new DeductionComponentCreateRequestDto
		{
			DeductionComponentName = dependentName,
			DeductionType = "FIXED",
			CalculationType = "PERCENTAGE",
			Percentage = 9m,
			SelectedDeductionComponentId = Guid.NewGuid(),
			VisibleInPayslip = true
		};

		var createResponse = await Client.PostAsJsonAsync(BaseUrl, createDependent, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

		var listResponse = await Client.GetAsync($"{BaseUrl}?search={dependentName}");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var body = await listResponse.Content.ReadFromJsonAsync<DeductionComponentPaginatedResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		var dependent = body.Items.FirstOrDefault(x => x.DeductionComponentName == dependentName);
		dependent.ShouldNotBeNull();
		dependent.SelectedDeductionComponentId.ShouldNotBeNull();
		dependent.CustomComponent.ShouldBeNull();
	}

	#endregion

	#region Tier 2 — Extended

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Handle_Pagination_With_Many_Components()
	{
		for (var i = 0; i < 25; i++)
		{
			await CreateComponentAsync($"BulkDeduction_{i}");
		}

		var page1 = await Client.GetAsync($"{BaseUrl}?page=1&pageSize=10");
		var body1 = await page1.Content.ReadFromJsonAsync<DeductionComponentPaginatedResponseDto>(JsonOptions);
		body1.ShouldNotBeNull();
		body1.Items.Count.ShouldBeLessThanOrEqualTo(10);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Create_Update_Delete_Full_Lifecycle()
	{
		var created = await CreateComponentAsync();

		var updateDto = new DeductionComponentUpdateRequestDto
		{
			DeductionComponentName = TestDataHelper.UniqueName("LifecycleUpd"),
			DeductionType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = 8000m,
			VisibleInPayslip = false,
		};
		var updateResponse = await PutWithPreviewAsync(created.Id, updateDto);
		updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{created.Id}");
		var body = await getResponse.Content.ReadFromJsonAsync<DeductionComponentResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Amount.ShouldBe(8000m);

		var deleteResponse = await DeleteWithPreviewAsync(created.Id);
		deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	#endregion

	#region Helpers

	private async Task<ComponentCreateResponseDto> CreateComponentAsync(string? nameSuffix = null)
	{
		var input = new DeductionComponentCreateRequestDto
		{
			DeductionComponentName = TestDataHelper.UniqueName(nameSuffix ?? "DeductComp"),
			DeductionType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = 3000m,
			VisibleInPayslip = true,
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<ComponentCreateResponseDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create test deduction component");
	}

	private async Task<HttpResponseMessage> PutWithPreviewAsync(Guid id, DeductionComponentUpdateRequestDto input)
	{
		var previewResponse = await Client.PostAsJsonAsync(
			$"{BaseUrl}/{id}/update-impact-preview",
			input,
			JsonOptions);
		previewResponse.EnsureSuccessStatusCode();
		var preview = await previewResponse.Content.ReadFromJsonAsync<PayrollComponentUpdateImpactPreviewDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to preview deduction component update");

		input.ApplyMode = "SyncedOnly";
		input.EffectiveMonth = preview.EffectiveMonth;
		input.PreviewHash = preview.PreviewHash;

		return await Client.PutAsJsonAsync($"{BaseUrl}/{id}", input, JsonOptions);
	}

	private async Task<HttpResponseMessage> DeleteWithPreviewAsync(Guid id)
	{
		var previewResponse = await Client.GetAsync($"{BaseUrl}/{id}/delete-impact-preview");
		previewResponse.EnsureSuccessStatusCode();
		var preview = await previewResponse.Content.ReadFromJsonAsync<PayrollComponentUpdateImpactPreviewDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to preview deduction component delete");

		return await Client.DeleteAsync($"{BaseUrl}/{id}?previewHash={Uri.EscapeDataString(preview.PreviewHash)}");
	}

	private async Task<(ComponentCreateResponseDto Created, string Name)> CreateComponentWithNameAsync(string nameSuffix)
	{
		var name = TestDataHelper.UniqueName(nameSuffix);
		var input = new DeductionComponentCreateRequestDto
		{
			DeductionComponentName = name,
			DeductionType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = 3000m,
			VisibleInPayslip = true,
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		response.EnsureSuccessStatusCode();
		var created = await response.Content.ReadFromJsonAsync<ComponentCreateResponseDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create test deduction component");
		return (created, name);
	}

	private async Task<(ComponentCreateResponseDto Created, string Name)> CreateEarningComponentWithNameAsync(string nameSuffix)
	{
		var name = TestDataHelper.UniqueName(nameSuffix);
		var input = new EarningComponentCreateRequestDto
		{
			EarningComponentName = name,
			EarningType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = 30000m,
			VisibleInPayslip = true,
		};

		var response = await Client.PostAsJsonAsync(EarningComponentsBaseUrl, input, JsonOptions);
		response.EnsureSuccessStatusCode();
		var created = await response.Content.ReadFromJsonAsync<ComponentCreateResponseDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create test earning component");
		return (created, name);
	}

	#endregion
}
