using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class PayrollApiDescription_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Fact]
	public async Task HR_document_describes_registration_and_multipart_logo_upload()
	{
		using var scope = Factory.Services.CreateScope();
		var document = scope.ServiceProvider.GetRequiredService<ISwaggerProvider>().GetSwagger("v1-hr");
		document.Paths.ShouldContainKey("/api/employees/{employeeId}/payroll/statutory-profile");
		var upload = document.Paths["/api/payroll/payslip-settings/logo"];
		upload.Operations.Values.ShouldContain(operation => operation.RequestBody.Content.ContainsKey("multipart/form-data"));

		using var response = await Client.GetAsync("/swagger/v1-hr/swagger.json");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		json.RootElement.GetProperty("paths").TryGetProperty("/api/payroll/payslip-settings/logo", out _).ShouldBeTrue();
	}

	[Fact]
	public async Task Logo_upload_binds_the_documented_multipart_file()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var upload = new MultipartFormDataContent();
		using var image = new ByteArrayContent(Convert.FromBase64String(
			"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVR4nGNgAAIAAAUAAXpeqz8AAAAASUVORK5CYII="));
		image.Headers.ContentType = new("image/png");
		upload.Add(image, "file", "synthetic-payroll-logo.png");
		using var response = await Client.PostAsync("/api/payroll/payslip-settings/logo", upload);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		json.RootElement.GetProperty("blobName").GetString().ShouldStartWith("payroll-logos/");
	}
}