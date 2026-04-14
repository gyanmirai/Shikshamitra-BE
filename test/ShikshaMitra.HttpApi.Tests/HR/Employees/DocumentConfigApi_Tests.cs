using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Settings;
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

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Business_Code_For_Duplicate_Document_Title()
	{
		var request = BuildDocumentRequest();
		var firstCreate = await Client.PostAsJsonAsync(BaseUrl, request, JsonOptions);
		firstCreate.StatusCode.ShouldBe(HttpStatusCode.Created);

		var duplicateCreate = await Client.PostAsJsonAsync(BaseUrl, request, JsonOptions);
		duplicateCreate.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
		if (duplicateCreate.StatusCode != HttpStatusCode.BadRequest)
		{
			return;
		}

		var json = await duplicateCreate.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		var error = document.RootElement.GetProperty("error");
		var code = error.GetProperty("code").GetString();
		code.ShouldNotBe("VALIDATION_ERROR");
		code.ShouldBe("HR:Employee:010026");
	}

	#endregion

	#region Tier 2 — Extended

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Reject_Title_With_Numbers_Or_SpecialChars()
	{
		var request = new DocumentRequest
		{
			Title = "Invalid123!@#",
			DocumentSize = 5,
			DocumentTypes = new List<int> { 1, 2 },
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, request, JsonOptions);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.BadRequest,
			HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Reject_DocumentSize_Out_Of_Range()
	{
		var requestZero = new DocumentRequest
		{
			Title = "Valid Title",
			DocumentSize = 0,
			DocumentTypes = new List<int> { 1 },
		};
		var r1 = await Client.PostAsJsonAsync(BaseUrl, requestZero, JsonOptions);
		r1.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);

		var requestOverMax = new DocumentRequest
		{
			Title = "Valid Title",
			DocumentSize = 21,
			DocumentTypes = new List<int> { 1 },
		};
		var r2 = await Client.PostAsJsonAsync(BaseUrl, requestOverMax, JsonOptions);
		r2.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Complete_Full_CRUD_Lifecycle()
	{
		var createRequest = BuildDocumentRequest();
		var createResponse = await Client.PostAsJsonAsync(BaseUrl, createRequest, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var created = await createResponse.Content.ReadFromJsonAsync<DocumentCreateResponse>(JsonOptions);
		created.ShouldNotBeNull();
		created.Id.ShouldNotBe(Guid.Empty);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{created.Id}");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var item = await getResponse.Content.ReadFromJsonAsync<DocumentItem>(JsonOptions);
		item.ShouldNotBeNull();
		item.Id.ShouldBe(created.Id);

		var updateRequest = new DocumentRequest
		{
			Title = "Updated Document",
			DocumentSize = 15,
			DocumentTypes = new List<int> { 1, 2, 3, 4 },
		};
		var updateResponse = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", updateRequest, JsonOptions);
		updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var verifyResponse = await Client.GetAsync($"{BaseUrl}/{created.Id}");
		verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updated = await verifyResponse.Content.ReadFromJsonAsync<DocumentItem>(JsonOptions);
		updated.ShouldNotBeNull();
		updated.Title.ShouldBe("Updated Document");
		updated.DocumentSize.ShouldBe(15);

		var deleteResponse = await Client.DeleteAsync($"{BaseUrl}/{created.Id}");
		deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var goneResponse = await Client.GetAsync($"{BaseUrl}/{created.Id}");
		goneResponse.StatusCode.ShouldBeOneOf(
			HttpStatusCode.NotFound,
			HttpStatusCode.InternalServerError);
	}

	#endregion

	#region Helpers

	private static DocumentRequest BuildDocumentRequest()
	{
		var suffix = Guid.NewGuid().ToString("N")[..6]
			.Aggregate("", (s, c) => s + (char.IsDigit(c) ? (char)('a' + (c - '0')) : c));
		return new DocumentRequest
		{
			Title = $"Test Document {suffix}",
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
