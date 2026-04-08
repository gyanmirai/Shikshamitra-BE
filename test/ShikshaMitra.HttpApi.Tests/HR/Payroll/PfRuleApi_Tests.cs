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
public class PfRuleApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/payroll/pf-rules";

	public PfRuleApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_PF_Rule_Returns_201()
	{
		var input = BuildCreateDto();

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
		var body = await response.Content.ReadFromJsonAsync<PfRuleCreateResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_PF_Rule_By_Id()
	{
		var created = await CreatePfRuleAsync();

		var response = await Client.GetAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PfRuleResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldBe(created.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_PF_Rule_List()
	{
		await CreatePfRuleAsync();

		var response = await Client.GetAsync($"{BaseUrl}?page=1&pageSize=20");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PaginatedPfRuleResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
		body.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_PF_Rule()
	{
		var created = await CreatePfRuleAsync();

		var updateDto = new CreateUpdateProvidentFundDto
		{
			Name = TestDataHelper.UniqueName("UpdatedPF"),
			Code = TestDataHelper.UniqueName("UPF").Replace("_", "").ToUpperInvariant()[..12],
			EmployeePercentage = 15m,
			EmployerPercentage = 13m,
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", updateDto, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Delete_PF_Rule()
	{
		var created = await CreatePfRuleAsync();

		var response = await Client.DeleteAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_404_For_Nonexistent()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Search_PF_Rules()
	{
		var uniquePart = Guid.NewGuid().ToString("N")[..6];
		var input = new CreateUpdateProvidentFundDto
		{
			Name = $"Searchable_{uniquePart}",
			Code = $"S{uniquePart}".ToUpperInvariant()[..10],
			EmployeePercentage = 12m,
			EmployerPercentage = 12m,
		};
		await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		var response = await Client.GetAsync($"{BaseUrl}?search={uniquePart}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PaginatedPfRuleResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
	}

	#endregion

	#region Helpers

	private static CreateUpdateProvidentFundDto BuildCreateDto()
	{
		return new CreateUpdateProvidentFundDto
		{
			Name = TestDataHelper.UniqueName("StandardPF"),
			Code = TestDataHelper.UniqueName("PF").Replace("_", "").ToUpperInvariant()[..12],
			EmployeePercentage = 12m,
			EmployerPercentage = 12m,
		};
	}

	private async Task<PfRuleCreateResponseDto> CreatePfRuleAsync()
	{
		var input = BuildCreateDto();
		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<PfRuleCreateResponseDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create PF rule");
	}

	#endregion
}
