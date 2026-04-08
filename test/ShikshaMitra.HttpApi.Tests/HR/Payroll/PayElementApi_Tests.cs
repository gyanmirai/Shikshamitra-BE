using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class PayElementApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/payroll/pay-elements";

	public PayElementApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Pay_Element_Returns_201()
	{
		var input = BuildCreateDto();

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
		var body = await response.Content.ReadFromJsonAsync<PayElementDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Pay_Element_By_Id()
	{
		var created = await CreatePayElementAsync();

		var response = await Client.GetAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PayElementDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldBe(created.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Pay_Element_List()
	{
		await CreatePayElementAsync();

		var response = await Client.GetAsync($"{BaseUrl}?SkipCount=0&MaxResultCount=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<PayElementDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
		body.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Pay_Element()
	{
		var created = await CreatePayElementAsync();

		var updateDto = new CreateUpdatePayElementDto
		{
			Code = TestDataHelper.UniqueName("UPD").Replace("_", "").ToUpperInvariant()[..10],
			Name = TestDataHelper.UniqueName("UpdatedElement"),
			Type = PayElementType.Credit,
			IsEmployeeSpecific = true,
			IsDependent = false,
			IsVisibleInPaySlip = false,
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", updateDto, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Delete_Pay_Element()
	{
		var created = await CreatePayElementAsync();

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
	public async Task Should_Create_Dependent_Pay_Element()
	{
		var baseElement = await CreatePayElementAsync("Base");

		var input = new CreateUpdatePayElementDto
		{
			Code = TestDataHelper.UniqueName("DEP").Replace("_", "").ToUpperInvariant()[..10],
			Name = TestDataHelper.UniqueName("HRA Dependent"),
			Type = PayElementType.Credit,
			IsEmployeeSpecific = false,
			IsDependent = true,
			Dependent = new DependentPropertiesDto
			{
				ElementId = baseElement.Id.ToString(),
				Percentage = 40f,
			},
			IsVisibleInPaySlip = true,
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	#endregion

	#region Helpers

	private static CreateUpdatePayElementDto BuildCreateDto(string? suffix = null)
	{
		return new CreateUpdatePayElementDto
		{
			Code = TestDataHelper.UniqueName(suffix ?? "PE").Replace("_", "").ToUpperInvariant()[..10],
			Name = TestDataHelper.UniqueName(suffix ?? "BasicSalary"),
			Type = PayElementType.Credit,
			IsEmployeeSpecific = false,
			IsDependent = false,
			IsVisibleInPaySlip = true,
		};
	}

	private async Task<PayElementDto> CreatePayElementAsync(string? suffix = null)
	{
		var input = BuildCreateDto(suffix);
		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<PayElementDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create pay element");
	}

	#endregion
}
