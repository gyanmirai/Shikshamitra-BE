using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.SchoolDocuments;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class SchoolDocumentsApi_Tests
{
	[Theory]
	[InlineData("none")]
	[InlineData("too-many")]
	[InlineData("empty")]
	[InlineData("too-large")]
	[InlineData("extension")]
	[InlineData("mime")]
	[InlineData("signature")]
	[InlineData("name")]
	public async Task Invalid_uploads_do_not_create_partial_records(string scenario)
	{
		using var client = await ClientAsync();
		using var form = Form(scenario == "name" ? " " : "Invalid approval",
			count: scenario == "none" ? 0 : scenario == "too-many" ? 6 : 1,
			bytes: scenario switch { "empty" => [], "too-large" => new byte[SchoolDocumentConsts.MaxFileBytes + 1], "signature" => "<html>fake</html>"u8.ToArray(), _ => Png },
			fileName: scenario == "extension" ? "approval.exe" : "approval.png",
			contentType: scenario == "mime" ? "text/html" : "image/png");
		var response = await client.PostAsync(Url, form);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		(await client.GetFromJsonAsync<PagedResultDto<SchoolDocumentDto>>(Url))!.TotalCount.ShouldBe(0);
	}

	[Fact]
	public async Task Persistence_failure_rolls_back_metadata_attachments_and_database_blobs()
	{
		using var client = await ClientAsync();
		using var form = Form("Failed approval", count: 1);
		var failure = new ByteArrayContent(Png);
		failure.Headers.ContentType = new("image/png");
		form.Add(failure, "files", "rollback-failure.png");
		(await client.PostAsync(Url, form)).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
		(await client.GetFromJsonAsync<PagedResultDto<SchoolDocumentDto>>(Url))!.TotalCount.ShouldBe(0);
		var counts = await factory.StorageCountsAsync(Guid.Parse(client.DefaultRequestHeaders.GetValues(TestAuthHandler.TestTenantIdHeader).Single()));
		counts.ShouldBe((0, 0, 0L));
		var saved = await CreateAsync(client, "Retry approval");
		saved.Files.Count.ShouldBe(1);
	}

	[Fact]
	public async Task View_only_cannot_create_update_delete_and_anonymous_cannot_read_files()
	{
		using var client = await ClientAsync();
		var saved = await CreateAsync(client, "Protected approval");
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, SchoolDocumentPermissions.View);
		using var create = Form("Blocked", count: 1);
		(await client.PostAsync(Url, create)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var update = Form("Blocked edit", saved, retained: [saved.Files[0].Id]);
		(await client.PutAsync($"{Url}/{saved.Id}", update)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.DeleteAsync($"{Url}/{saved.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, SchoolDocumentPermissions.Add);
		(await client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.GetAsync($"{Url}/{saved.Id}/files/{saved.Files[0].Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		client.DefaultRequestHeaders.Add("X-Skip-Auth", "true");
		(await client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}
}
