using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.SchoolDocuments;
using Shouldly;
using Xunit;

namespace ShikshaMitra.TenantSettings;

[Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public class SchoolDocumentsPostgresApi_Tests
{
	private const string Url = "/api/institute-setup/school-documents";
	private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aRZkAAAAASUVORK5CYII=");

	[SchoolDocumentsPostgresFact]
	public async Task Additive_migration_blob_rollback_concurrent_updates_and_retention_work_in_postgresql()
	{
		await using var factory = new SchoolDocumentsPostgresFactory();
		var tenant = Guid.NewGuid();
		await factory.SeedTenantAsync(tenant);
		using var client = factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenant.ToString());
		using (var failure = Form())
		{
			AddFile(failure, "rollback-failure.png");
			(await client.PostAsync(Url, failure)).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
		}
		(await factory.StorageCountsAsync(tenant)).ShouldBe((0, 0, 0L));
		SchoolDocumentDto saved;
		using (var create = Form())
		{
			var response = await client.PostAsync(Url, create);
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			saved = (await response.Content.ReadFromJsonAsync<SchoolDocumentDto>())!;
		}
		(await factory.StorageCountsAsync(tenant)).ShouldBe((1, 1, 1L));
		using var one = Form(saved);
		using var two = Form(saved);
		var race = await Task.WhenAll(client.PutAsync($"{Url}/{saved.Id}", one), client.PutAsync($"{Url}/{saved.Id}", two));
		race.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
		race.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
		(await client.GetByteArrayAsync($"{Url}/{saved.Id}/files/{saved.Files[0].Id}")).ShouldBe(Png);
		(await client.DeleteAsync($"{Url}/{saved.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
		(await factory.StorageCountsAsync(tenant)).ShouldBe((1, 1, 1L));
		(await client.GetAsync($"{Url}/{saved.Id}/files/{saved.Files[0].Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		await factory.VerifyDownAsync();
	}

	private static MultipartFormDataContent Form(SchoolDocumentDto? saved = null)
	{
		var form = new MultipartFormDataContent();
		form.Add(new StringContent("Synthetic government approval"), "name");
		form.Add(new StringContent("0"), "category");
		if (saved == null) AddFile(form, "approval.png");
		else
		{
			form.Add(new StringContent(saved.ConcurrencyStamp), "concurrencyStamp");
			form.Add(new StringContent(saved.Files[0].Id.ToString()), "retainedFileIds");
		}
		return form;
	}

	private static void AddFile(MultipartFormDataContent form, string name)
	{
		var file = new ByteArrayContent(Png);
		file.Headers.ContentType = new("image/png");
		form.Add(file, "files", name);
	}
}
