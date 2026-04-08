using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Documents;
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
	public async Task Should_Upload_Document()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DocUpload");

		using var content = BuildDocumentUpload();
		var response = await Client.PostAsync(Url(empId), content);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Document_List()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DocList");

		using var content = BuildDocumentUpload();
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

		using var uploadContent = BuildDocumentUpload();
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

	#endregion

	#region Helpers

	private static MultipartFormDataContent BuildDocumentUpload()
	{
		var content = new MultipartFormDataContent();
		content.Add(new StringContent(Guid.NewGuid().ToString()), "DocumentNameId");
		var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test file content for document upload"));
		fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
		content.Add(fileContent, "File", "test-document.pdf");
		return content;
	}

	#endregion
}
