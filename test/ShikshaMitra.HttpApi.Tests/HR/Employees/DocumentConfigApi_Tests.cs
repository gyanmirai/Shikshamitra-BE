using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class DocumentConfigApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/employees/documents";

	public DocumentConfigApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Document_Config()
	{
		var request = BuildDocumentRequest();

		var response = await Client.PostAsJsonAsync(BaseUrl, request, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
		var body = await response.Content.ReadFromJsonAsync<DocumentCreateResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Document_Config_List()
	{
		await CreateDocConfigAsync();

		var response = await Client.GetAsync($"{BaseUrl}?page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<DocumentListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
		body.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Document_Config_By_Id()
	{
		var created = await CreateDocConfigAsync();

		var response = await Client.GetAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<DocumentItem>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldBe(created.Id);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Update_Document_Config()
	{
		var created = await CreateDocConfigAsync();

		var updateRequest = new DocumentRequest
		{
			Title = "Updated Card",
			DocumentSize = 10,
			DocumentTypes = new List<int> { 1, 2, 3 },
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", updateRequest, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Delete_Document_Config()
	{
		var created = await CreateDocConfigAsync();

		var response = await Client.DeleteAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_404_For_Nonexistent()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.NotFound,
			HttpStatusCode.InternalServerError);
	}

	#endregion

	#region Helpers

	private static DocumentRequest BuildDocumentRequest()
	{
		return new DocumentRequest
		{
			Title = TestDataHelper.UniqueName("AadhaarCard").Replace("_", " ").Replace(".", "")[..30],
			DocumentSize = 5,
			DocumentTypes = new List<int> { 1, 2 },
		};
	}

	private async Task<DocumentCreateResponse> CreateDocConfigAsync()
	{
		var request = BuildDocumentRequest();
		var response = await Client.PostAsJsonAsync(BaseUrl, request, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<DocumentCreateResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create document config");
	}

	#endregion
}
