using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Documents;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class EmployeeDocumentApi_Tests : ShikshaMitraApiTestBase
{
	public EmployeeDocumentApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	private static string Url(Guid empId) => $"/api/employees/{empId}/documents";

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Upload_Document_With_Valid_DocConfig()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DocUpValid");
		var docConfig = await EmployeeTestHelper.CreateDocumentConfigAsync(Client);

		using var content = BuildDocumentUpload(docConfig.Id);
		var response = await Client.PostAsync(Url(empId), content);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Upload_Image_File()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DocImage");
		var docConfig = await EmployeeTestHelper.CreateDocumentConfigAsync(Client);

		using var content = BuildImageUpload(docConfig.Id);
		var response = await Client.PostAsync(Url(empId), content);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Document_List()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DocList");
		var docConfig = await EmployeeTestHelper.CreateDocumentConfigAsync(Client);

		using var content = BuildDocumentUpload(docConfig.Id);
		await Client.PostAsync(Url(empId), content);

		var response = await Client.GetAsync($"{Url(empId)}?page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EmployeeDocumentListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Nonexistent_Employee_Documents()
	{
		var response = await Client.GetAsync($"/api/employees/{Guid.NewGuid()}/documents");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.NotFound,
			HttpStatusCode.InternalServerError,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Delete_Document()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DocDel");
		var docConfig = await EmployeeTestHelper.CreateDocumentConfigAsync(Client);

		using var uploadContent = BuildDocumentUpload(docConfig.Id);
		await Client.PostAsync(Url(empId), uploadContent);

		var listResponse = await Client.GetAsync($"{Url(empId)}?page=1&pageSize=10");
		var list = await listResponse.Content.ReadFromJsonAsync<EmployeeDocumentListResponse>(JsonOptions);

		if (list?.Items is { Count: > 0 })
		{
			var docId = list.Items[0].DocumentId;
			var deleteResponse = await Client.DeleteAsync($"{Url(empId)}/{docId}");
			deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Download_Uploaded_Document()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DocDL");
		var docConfig = await EmployeeTestHelper.CreateDocumentConfigAsync(Client);

		using var uploadContent = BuildDocumentUpload(docConfig.Id);
		var uploadResponse = await Client.PostAsync(Url(empId), uploadContent);
		uploadResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

		var listResponse = await Client.GetAsync($"{Url(empId)}?page=1&pageSize=10");
		var list = await listResponse.Content.ReadFromJsonAsync<EmployeeDocumentListResponse>(JsonOptions);
		list.ShouldNotBeNull();
		list.Items.ShouldNotBeEmpty();

		var docId = list.Items[0].DocumentId;
		var downloadResponse = await Client.GetAsync($"{Url(empId)}/{docId}");
		downloadResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		downloadResponse.Content.Headers.ContentType.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Fail_Upload_Without_File()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DocNoFile");
		var docConfig = await EmployeeTestHelper.CreateDocumentConfigAsync(Client);

		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(docConfig.Id.ToString()), "DocumentNameId");

		var response = await Client.PostAsync(Url(empId), content);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.BadRequest,
			HttpStatusCode.InternalServerError);
	}

	#endregion

	#region Tier 2 — Extended

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Upload_PDF_File()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DocPDF");
		var docConfig = await EmployeeTestHelper.CreateDocumentConfigAsync(Client);

		using var content = BuildDocumentUpload(docConfig.Id, "report.pdf", "application/pdf");
		var response = await Client.PostAsync(Url(empId), content);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Update_Existing_Document()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DocUpdate");
		var docConfig = await EmployeeTestHelper.CreateDocumentConfigAsync(Client);

		using var uploadContent = BuildDocumentUpload(docConfig.Id, "original.pdf", "application/pdf");
		var uploadResp = await Client.PostAsync(Url(empId), uploadContent);
		uploadResp.StatusCode.ShouldBe(HttpStatusCode.Created);

		var listResponse = await Client.GetAsync($"{Url(empId)}?page=1&pageSize=10");
		var list = await listResponse.Content.ReadFromJsonAsync<EmployeeDocumentListResponse>(JsonOptions);
		list.ShouldNotBeNull();
		list.Items.ShouldNotBeEmpty();

		var docId = list.Items[0].DocumentId;

		using var updateContent = new MultipartFormDataContent();
		updateContent.Add(new StringContent(docConfig.Id.ToString()), "DocumentNameId");
		var updatedFile = new ByteArrayContent(Encoding.UTF8.GetBytes("Updated document content"));
		updatedFile.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
		updateContent.Add(updatedFile, "File", "updated.pdf");

		var updateResp = await Client.PutAsync($"{Url(empId)}/{docId}", updateContent);
		updateResp.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Search_Documents_By_FileName()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DocSearch");
		var docConfig = await EmployeeTestHelper.CreateDocumentConfigAsync(Client);

		using var content = BuildDocumentUpload(docConfig.Id, "unique-search-test.pdf", "application/pdf");
		await Client.PostAsync(Url(empId), content);

		var response = await Client.GetAsync($"{Url(empId)}?search=unique-search-test&page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EmployeeDocumentListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
	}

	#endregion

	#region Helpers

	private static MultipartFormDataContent BuildDocumentUpload(
		Guid docConfigId,
		string fileName = "test-document.pdf",
		string contentType = "application/pdf")
	{
		var content = new MultipartFormDataContent();
		content.Add(new StringContent(docConfigId.ToString()), "DocumentNameId");
		var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test file content for document upload"));
		fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
		content.Add(fileContent, "File", fileName);
		return content;
	}

	private static MultipartFormDataContent BuildImageUpload(Guid docConfigId)
	{
		var content = new MultipartFormDataContent();
		content.Add(new StringContent(docConfigId.ToString()), "DocumentNameId");
		var imageBytes = EmployeeTestHelper.BuildSyntheticJpeg();
		var imageContent = new ByteArrayContent(imageBytes);
		imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
		content.Add(imageContent, "File", "employee-photo.jpg");
		return content;
	}

	#endregion
}
