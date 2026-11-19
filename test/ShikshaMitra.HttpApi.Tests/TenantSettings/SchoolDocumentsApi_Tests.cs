using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.SchoolDocuments;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.TenantSettings;

[Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public partial class SchoolDocumentsApi_Tests(SchoolDocumentsTestFactory factory) : IClassFixture<SchoolDocumentsTestFactory>
{
	private const string Url = "/api/institute-setup/school-documents";
	private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aRZkAAAAASUVORK5CYII=");

	[Fact]
	public async Task Multiple_files_persist_preview_download_search_and_concurrent_edit()
	{
		using var client = await ClientAsync();
		var saved = await CreateAsync(client, "Government Approval", 2);
		saved.Files.Count.ShouldBe(2);
		(await client.GetFromJsonAsync<SchoolDocumentDto>($"{Url}/{saved.Id}"))!.Files.Count.ShouldBe(2);
		var list = (await client.GetFromJsonAsync<PagedResultDto<SchoolDocumentDto>>(Url + "?search=approval&category=0&maxResultCount=1&sorting=name%20asc"))!;
		list.TotalCount.ShouldBe(1);
		list.Items.Single().Id.ShouldBe(saved.Id);
		foreach (var file in saved.Files)
		{
			var response = await client.GetAsync($"{Url}/{saved.Id}/files/{file.Id}");
			response.StatusCode.ShouldBe(HttpStatusCode.OK);
			(await response.Content.ReadAsByteArrayAsync()).ShouldBe(Png);
			response.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
			response.Headers.CacheControl!.NoStore.ShouldBeTrue();
			response.Headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
			var download = await client.GetAsync($"{Url}/{saved.Id}/files/{file.Id}?download=true");
			download.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
		}
		using var change = Form("Updated Approval", saved, retained: saved.Files.Select(x => x.Id).ToArray());
		var updated = await client.PutAsync($"{Url}/{saved.Id}", change);
		updated.StatusCode.ShouldBe(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
		using var stale = Form("Stale overwrite", saved, retained: saved.Files.Select(x => x.Id).ToArray());
		(await client.PutAsync($"{Url}/{saved.Id}", stale)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await client.GetFromJsonAsync<SchoolDocumentDto>($"{Url}/{saved.Id}"))!.Name.ShouldBe("Updated Approval");
	}

	[Fact]
	public async Task Tenant_and_parent_ids_cannot_expose_or_attach_unrelated_files()
	{
		using var first = await ClientAsync();
		using var second = await ClientAsync();
		var a = await CreateAsync(first, "First school's approval");
		var b = await CreateAsync(second, "Second school's approval");
		(await second.GetAsync($"{Url}/{a.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await second.GetAsync($"{Url}/{a.Id}/files/{a.Files[0].Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await first.GetAsync($"{Url}/{a.Id}/files/{b.Files[0].Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		using var forged = Form("Forged file", a, retained: [b.Files[0].Id]);
		(await first.PutAsync($"{Url}/{a.Id}", forged)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await second.DeleteAsync($"{Url}/{a.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		using var host = factory.CreateClient();
		host.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, "host");
		(await host.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task File_removal_requires_delete_permission_and_soft_delete_blocks_file_access()
	{
		using var client = await ClientAsync();
		var saved = await CreateAsync(client, "Permission NOC", 2);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			SchoolDocumentPermissions.View + "," + SchoolDocumentPermissions.Add);
		using var denied = Form(saved.Name, saved, retained: [saved.Files[0].Id]);
		(await client.PutAsync($"{Url}/{saved.Id}", denied)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		using var allowed = Form(saved.Name, saved, retained: [saved.Files[0].Id]);
		(await client.PutAsync($"{Url}/{saved.Id}", allowed)).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await client.GetAsync($"{Url}/{saved.Id}/files/{saved.Files[1].Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await client.DeleteAsync($"{Url}/{saved.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
		(await client.GetAsync($"{Url}/{saved.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await client.GetAsync($"{Url}/{saved.Id}/files/{saved.Files[0].Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await client.GetFromJsonAsync<PagedResultDto<SchoolDocumentDto>>(Url))!.TotalCount.ShouldBe(0);
	}

	private async Task<HttpClient> ClientAsync()
	{
		var tenantId = Guid.NewGuid();
		await factory.SeedTenantAsync(tenantId);
		var client = factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenantId.ToString());
		return client;
	}

	private static async Task<SchoolDocumentDto> CreateAsync(HttpClient client, string name, int count = 1)
	{
		using var form = Form(name, count: count);
		var response = await client.PostAsync(Url, form);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<SchoolDocumentDto>())!;
	}

	private static MultipartFormDataContent Form(string name, SchoolDocumentDto? original = null, int count = 0, Guid[]? retained = null,
		byte[]? bytes = null, string fileName = "approval.png", string contentType = "image/png")
	{
		var form = new MultipartFormDataContent();
		form.Add(new StringContent(name), "name");
		form.Add(new StringContent("0"), "category");
		form.Add(new StringContent(original?.ConcurrencyStamp ?? ""), "concurrencyStamp");
		foreach (var id in retained ?? []) form.Add(new StringContent(id.ToString()), "retainedFileIds");
		for (var index = 0; index < count; index++)
		{
			var file = new ByteArrayContent(bytes ?? Png);
			file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
			form.Add(file, "files", fileName);
		}
		return form;
	}
}
