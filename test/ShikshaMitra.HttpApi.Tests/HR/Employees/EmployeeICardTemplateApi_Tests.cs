using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.ICards;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class EmployeeICardTemplateApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/employees/i-card/templates";

	public EmployeeICardTemplateApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Upload_And_Download_Icard_Asset()
	{
		var upload = await UploadLogoAsync(BuildMinimalPng(), "logo.png", "image/png");

		var response = await Client.GetAsync($"{BaseUrl}/assets?blobName={Uri.EscapeDataString(upload.BlobName)}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		response.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
		(await response.Content.ReadAsByteArrayAsync()).Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Arbitrary_Icard_Asset_Blob_Name()
	{
		var response = await Client.GetAsync($"{BaseUrl}/assets?blobName={Uri.EscapeDataString("employee-documents/logo.png")}");

		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Oversized_Icard_Asset_Upload()
	{
		var response = await UploadLogoResponseAsync(new byte[(2 * 1024 * 1024) + 1], "logo.png", "image/png");

		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Generate_Icard_Pdf_For_Selected_Employee()
	{
		var template = await CreateTemplateAsync();
		var employeeId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "ICardPdf");

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}/{template.Id}/pdf",
			new EmployeeICardPdfRequest { EmployeeIds = [employeeId], CardsPerPage = 8 },
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
		var bytes = await response.Content.ReadAsByteArrayAsync();
		bytes.Length.ShouldBeGreaterThan(100);
		bytes[0].ShouldBe((byte)'%');
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Icard_Pdf_For_Unknown_Employee()
	{
		var template = await CreateTemplateAsync();

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}/{template.Id}/pdf",
			new EmployeeICardPdfRequest { EmployeeIds = [Guid.NewGuid()], CardsPerPage = 8 },
			JsonOptions);

		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
	}

	private async Task<EmployeeICardTemplateDto> CreateTemplateAsync()
	{
		var response = await Client.PostAsJsonAsync(BaseUrl, new EmployeeICardTemplateRequest
		{
			AcademicYearId = Guid.NewGuid(),
			Name = $"I-Card {Guid.NewGuid():N}",
			Width = 360,
			Height = 230,
			SchoolName = "API School",
			SchoolAddress = "API Address",
			ThemeColor = "#1D4ED8",
			BackgroundColor = "#F8FAFC",
			Orientation = "Horizontal",
			PresetKey = "executive-clean",
			QrCodeEnabled = true,
			BarcodeEnabled = false,
			BackSideEnabled = false,
			FieldsJson = """[{"name":"photo","enabled":true},{"name":"name","enabled":true},{"name":"employeeCode","enabled":true}]""",
			IsActive = true
		}, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<EmployeeICardTemplateDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create I-card template");
	}

	private async Task<EmployeeICardAssetUploadResult> UploadLogoAsync(byte[] bytes, string fileName, string contentType)
	{
		var response = await UploadLogoResponseAsync(bytes, fileName, contentType);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<EmployeeICardAssetUploadResult>(JsonOptions)
			?? throw new InvalidOperationException("Failed to upload I-card asset");
	}

	private async Task<HttpResponseMessage> UploadLogoResponseAsync(byte[] bytes, string fileName, string contentType)
	{
		using var content = new MultipartFormDataContent();
		var imageContent = new ByteArrayContent(bytes);
		imageContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
		content.Add(imageContent, "file", fileName);
		return await Client.PostAsync($"{BaseUrl}/assets", content);
	}

	private static byte[] BuildMinimalPng()
	{
		return
		[
			0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
			0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
			0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
			0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
			0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
			0x54, 0x78, 0x9C, 0x62, 0x00, 0x00, 0x00, 0x02,
			0x00, 0x01, 0xE5, 0x27, 0xDE, 0xFC, 0x00, 0x00,
			0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42,
			0x60, 0x82
		];
	}
}
