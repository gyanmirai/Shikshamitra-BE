using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.Documents;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class EmployeeDocumentApi_Tests : ShikshaMitraApiTestBase
{
	public EmployeeDocumentApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	/// <summary>Minimal PDF body (magic bytes %PDF) for <see cref="FileValidationService.ValidateDocumentFileSignature"/>.</summary>
	private static readonly byte[] MinimalPdfBytes =
	[
		0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A,
		0x31, 0x20, 0x30, 0x20, 0x6F, 0x62, 0x6A, 0x0A, 0x3C, 0x3C, 0x2F, 0x54, 0x79, 0x70, 0x65, 0x2F, 0x43, 0x61, 0x74, 0x61, 0x6C, 0x6F, 0x67, 0x2F, 0x50, 0x61, 0x67, 0x65, 0x73, 0x20, 0x32, 0x20, 0x30, 0x20, 0x52, 0x3E, 0x3E, 0x0A, 0x65, 0x6E, 0x64, 0x6F, 0x62, 0x6A, 0x0A,
		0x32, 0x20, 0x30, 0x20, 0x6F, 0x62, 0x6A, 0x0A, 0x3C, 0x3C, 0x2F, 0x54, 0x79, 0x70, 0x65, 0x2F, 0x50, 0x61, 0x67, 0x65, 0x73, 0x2F, 0x4B, 0x69, 0x64, 0x73, 0x5B, 0x33, 0x20, 0x30, 0x20, 0x52, 0x5D, 0x2F, 0x43, 0x6F, 0x75, 0x6E, 0x74, 0x20, 0x31, 0x3E, 0x3E, 0x0A, 0x65, 0x6E, 0x64, 0x6F, 0x62, 0x6A, 0x0A,
		0x33, 0x20, 0x30, 0x20, 0x6F, 0x62, 0x6A, 0x0A, 0x3C, 0x3C, 0x2F, 0x54, 0x79, 0x70, 0x65, 0x2F, 0x50, 0x61, 0x67, 0x65, 0x2F, 0x50, 0x61, 0x72, 0x65, 0x6E, 0x74, 0x20, 0x32, 0x20, 0x30, 0x20, 0x52, 0x3E, 0x3E, 0x0A, 0x65, 0x6E, 0x64, 0x6F, 0x62, 0x6A, 0x0A,
		0x78, 0x72, 0x65, 0x66, 0x0A, 0x30, 0x20, 0x34, 0x0A, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x20, 0x36, 0x35, 0x35, 0x33, 0x35, 0x20, 0x66, 0x20, 0x0A, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x31, 0x30, 0x20, 0x30, 0x30, 0x30, 0x30, 0x30, 0x20, 0x6E, 0x20, 0x0A, 0x74, 0x72, 0x61, 0x69, 0x6C, 0x65, 0x72, 0x0A, 0x3C, 0x3C, 0x2F, 0x53, 0x69, 0x7A, 0x65, 0x20, 0x34, 0x3E, 0x3E, 0x0A, 0x73, 0x74, 0x61, 0x72, 0x74, 0x78, 0x72, 0x65, 0x66, 0x0A, 0x39, 0x39, 0x0A, 0x25, 0x25, 0x45, 0x4F, 0x46,
	];

	private static string Url(Guid empId) => $"/api/employees/{empId}/documents";
	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Checklist_Should_Page_All_Definitions_And_Include_Missing_Uploads()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "ChecklistStaff");
		var prefix = "Export checklist " + new string(Guid.NewGuid().ToByteArray().Select(x => (char)('A' + x % 26)).ToArray());
		for (var index = 0; index < 23; index++)
		{
			var created = await Client.PostAsJsonAsync("/api/employees/documents", new DocumentRequest {
				Title = prefix + (char)('A' + index), DocumentSize = 5, DocumentTypes = [4]
			}, JsonOptions);
			created.EnsureSuccessStatusCode();
		}
		var first = await Client.GetFromJsonAsync<Volo.Abp.Application.Dtos.PagedResultDto<EmployeeDocumentChecklistItemDto>>(
			Url(employee) + "/checklist?page=1&pageSize=10&search=" + Uri.EscapeDataString(prefix), JsonOptions);
		var last = await Client.GetFromJsonAsync<Volo.Abp.Application.Dtos.PagedResultDto<EmployeeDocumentChecklistItemDto>>(
			Url(employee) + "/checklist?page=3&pageSize=10&search=" + Uri.EscapeDataString(prefix), JsonOptions);
		first!.TotalCount.ShouldBe(23); first.Items.Count.ShouldBe(10); last!.Items.Count.ShouldBe(3);
		first.Items.ShouldAllBe(x => x.EmployeeId == employee && x.StatusVal == "Not Uploaded" && x.UploadedDoc == null);
		first.Items.Select(x => x.Id).Intersect(last.Items.Select(x => x.Id)).ShouldBeEmpty();
	}

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
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
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
		downloadResponse.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
		(await downloadResponse.Content.ReadAsByteArrayAsync()).ShouldBe(MinimalPdfBytes);
	}

	[Fact, TestTier(TestTraits.Tiers.Important)]
	public async Task Selected_download_should_not_resolve_a_document_from_another_employee()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var first = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DocSelectedFirst");
		var second = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DocSelectedSecond");
		var definition = await EmployeeTestHelper.CreateDocumentConfigAsync(Client);
		using var upload = BuildDocumentUpload(definition.Id, "selected-only.pdf");
		(await Client.PostAsync(Url(first), upload)).EnsureSuccessStatusCode();
		var list = await Client.GetFromJsonAsync<EmployeeDocumentListResponse>(Url(first) + "?page=1&pageSize=10", JsonOptions);
		var documentId = list!.Items.Single().DocumentId;
		var rejected = await Client.GetAsync($"{Url(second)}/{documentId}");
		rejected.IsSuccessStatusCode.ShouldBeFalse();
		(await rejected.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.EmployeeDocumentNotFound);
		var selected = await Client.GetAsync($"{Url(first)}/{documentId}");
		selected.EnsureSuccessStatusCode();
		(await selected.Content.ReadAsByteArrayAsync()).ShouldBe(MinimalPdfBytes);
		using var foreignTenant = new HttpRequestMessage(HttpMethod.Get, $"{Url(first)}/{documentId}");
		foreignTenant.Headers.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		var tenantDenied = await Client.SendAsync(foreignTenant);
		tenantDenied.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
		(await tenantDenied.Content.ReadAsByteArrayAsync()).ShouldNotBe(MinimalPdfBytes);
	}

	[Fact, TestTier(TestTraits.Tiers.Important)]
	public async Task Checklist_should_keep_latest_upload_and_definition_page_counts()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DocLatest");
		var definition = await EmployeeTestHelper.CreateDocumentConfigAsync(Client);
		using (var first = BuildDocumentUpload(definition.Id, "older.pdf"))
			(await Client.PostAsync(Url(employee), first)).EnsureSuccessStatusCode();
		using (var latest = BuildDocumentUpload(definition.Id, "latest.pdf"))
			(await Client.PostAsync(Url(employee), latest)).EnsureSuccessStatusCode();
		var checklist = await Client.GetFromJsonAsync<Volo.Abp.Application.Dtos.PagedResultDto<EmployeeDocumentChecklistItemDto>>(
			Url(employee) + "/checklist?page=1&pageSize=100", JsonOptions);
		var row = checklist!.Items.Single(item => item.Id == definition.Id);
		row.StatusVal.ShouldBe("Uploaded");
		row.UploadedDoc!.FileName.ShouldBe("latest.pdf");
		row.UploadedDoc.Size.ShouldBe(MinimalPdfBytes.Length);
		var page = await Client.GetFromJsonAsync<Volo.Abp.Application.Dtos.PagedResultDto<EmployeeDocumentChecklistItemDto>>(
			Url(employee) + "/checklist?page=1&pageSize=1", JsonOptions);
		page!.TotalCount.ShouldBe(checklist.TotalCount);
		page.Items.Count.ShouldBe(1);
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
		var updatedFile = new ByteArrayContent(MinimalPdfBytes);
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
		var bytes = contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
			? MinimalPdfBytes
			: Encoding.UTF8.GetBytes("Test file content for document upload");
		var fileContent = new ByteArrayContent(bytes);
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
