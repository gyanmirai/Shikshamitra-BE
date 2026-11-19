using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Records;

// Only the application boundary is substituted for binding/limit assertions.
// Real MVC/ApiExplorer/Swagger/auth middleware and controller code are retained.
public sealed class StudentPhotoMultipartFactory : ShikshaMitraApiTestFactory
{
	public IMobileStudentPhotoAppService Photos { get; } = Substitute.For<IMobileStudentPhotoAppService>();

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureTestServices(services =>
		{
			services.RemoveAll<IMobileStudentPhotoAppService>();
			services.AddSingleton(Photos);
		});
	}
}

[Collection(ApiTestCollection.Name)]
public class StudentUploadSwaggerApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Fact]
	public async Task Student_swagger_generates_all_upload_actions_and_the_mobile_file_binary_contract()
	{
		// Swashbuckle6.8.1 explicitly requires inferred FormFile rather than [FromForm]
		// on a direct IFormFile parameter. Do not hide the endpoint to make this pass.
		// https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/v6.8.1/README.md#handle-forms-and-file-uploads
		using var response = await Client.GetAsync("/swagger/v1-students/swagger.json");
		var text = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
		using var document = JsonDocument.Parse(text);
		var paths = document.RootElement.GetProperty("paths");
		var upload = paths.GetProperty("/api/mobile/v1/students/{studentId}/profile/photo/drafts").GetProperty("post");
		var schema = upload.GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data").GetProperty("schema");
		var file = schema.GetProperty("properties").GetProperty("file");
		file.GetProperty("type").GetString().ShouldBe("string");
		file.GetProperty("format").GetString().ShouldBe("binary");
		paths.TryGetProperty("/api/students/records/{studentId}/photo", out _).ShouldBeTrue();
		paths.TryGetProperty("/api/students/records/changes/students/{studentId}/photo-drafts", out _).ShouldBeTrue();
		paths.TryGetProperty("/api/student/onboarding/{caseId}/documents", out _).ShouldBeTrue();
	}

	[Theory]
	[InlineData(false, HttpStatusCode.Forbidden)]
	[InlineData(true, HttpStatusCode.NotFound)]
	public async Task Mobile_photo_keeps_unlinked_ownership_and_unknown_tenant_denials(bool unknownTenant, HttpStatusCode expected)
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			$"{ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.View},{ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.Add}");
		if (unknownTenant) client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		using var body = StudentPhotoMultipartApi_Tests.Multipart([1], "file");
		using var response = await client.PostAsync($"/api/mobile/v1/students/{Guid.NewGuid()}/profile/photo/drafts", body);
		// Portal ownership precedes image validation; ABP rejects an unknown tenant before the controller.
		response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync());
	}
}

[Collection(ApiTestCollection.Name)]
public class StudentPhotoMultipartApi_Tests : IClassFixture<StudentPhotoMultipartFactory>
{
	private const int MaxBytes = 3 * 1024 * 1024;
	private readonly StudentPhotoMultipartFactory factory;
	public StudentPhotoMultipartApi_Tests(StudentPhotoMultipartFactory factory)
	{
		this.factory = factory;
		factory.Photos.ClearReceivedCalls();
		factory.Photos.UploadAsync(Arg.Any<Guid>(), Arg.Any<UploadStudentPhotoDto>())
			.Returns(new StudentRecordPhotoDraftDto { Id = Guid.NewGuid(), PhotoUrl = "synthetic-draft" });
	}

	[Theory]
	[InlineData(1)]
	[InlineData(MaxBytes)]
	public async Task Mobile_photo_http_binds_the_exact_file_part_and_preserves_binary_metadata(int length)
	{
		var studentId = Guid.NewGuid();
		var bytes = new byte[length]; bytes[0] = 37; bytes[^1] = 93;
		using var client = PermissionClient("both");
		using var body = Multipart(bytes, "file");
		using var response = await client.PostAsync(Url(studentId), body);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		await factory.Photos.Received(1).UploadAsync(studentId, Arg.Is<UploadStudentPhotoDto>(input =>
			input.FileName == "synthetic-photo.png" && input.ContentType == "image/png" && input.Content.SequenceEqual(bytes)));
	}

	[Theory]
	[InlineData("missing")]
	[InlineData("wrong-name")]
	[InlineData("extra-file")]
	[InlineData("empty")]
	[InlineData("too-large")]
	public async Task Mobile_photo_http_rejects_invalid_multipart_without_delegating(string kind)
	{
		using var client = PermissionClient("both");
		using var body = kind == "missing" ? new MultipartFormDataContent()
			: Multipart(new byte[kind == "too-large" ? MaxBytes + 1 : kind == "empty" ? 0 : 1], kind == "wrong-name" ? "photo" : "file");
		if (kind == "missing") body.Add(new StringContent("no file"), "description");
		if (kind == "extra-file") body.Add(new ByteArrayContent([2]), "other", "second.png");
		using var response = await client.PostAsync(Url(Guid.NewGuid()), body);
		var responseBody = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, responseBody);
		response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json", responseBody);
		await factory.Photos.DidNotReceiveWithAnyArgs().UploadAsync(default, default!);
		using var json = JsonDocument.Parse(responseBody);
		json.RootElement.TryGetProperty("errorCode", out var errorCode).ShouldBeTrue(responseBody);
		errorCode.GetString().ShouldBe(kind switch
		{
			"too-large" => "Students:Records:PhotoTooLarge",
			"missing" or "wrong-name" => "VALIDATION_ERROR",
			_ => "Students:Records:InvalidPhoto"
		}, responseBody);
	}

	[Theory]
	[InlineData("anonymous", HttpStatusCode.Unauthorized)]
	[InlineData("none", HttpStatusCode.Forbidden)]
	[InlineData("view", HttpStatusCode.Forbidden)]
	[InlineData("add", HttpStatusCode.Forbidden)]
	public async Task Mobile_photo_http_requires_authenticated_profile_view_and_add_before_upload(string grants, HttpStatusCode expected)
	{
		using var client = PermissionClient(grants);
		using var body = Multipart([1], "file");
		using var response = await client.PostAsync(Url(Guid.NewGuid()), body);
		response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync());
		await factory.Photos.DidNotReceiveWithAnyArgs().UploadAsync(default, default!);
	}

	internal static MultipartFormDataContent Multipart(byte[] bytes, string field)
	{
		var body = new MultipartFormDataContent();
		var file = new ByteArrayContent(bytes); file.Headers.ContentType = new("image/png");
		body.Add(file, field, "synthetic-photo.png");
		return body;
	}
	private static string Url(Guid id) => $"/api/mobile/v1/students/{id}/profile/photo/drafts";
	private HttpClient PermissionClient(string grants)
	{
		var client = factory.CreateClient();
		if (grants == "anonymous")
		{
			client.DefaultRequestHeaders.Add("X-Skip-Auth", "true");
			// This test checker grants all permissions when the header is absent.
			client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");
		}
		else client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, grants switch
		{
			"both" => $"{ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.View},{ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.Add}",
			"view" => ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.View,
			"add" => ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.Add,
			_ => "__none__"
		});
		return client;
	}
}
