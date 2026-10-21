using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.HostTenants;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class SchoolIdentityPostgresApi_Tests
{
	private const string Url = "/api/institute-setup/school-identity";
	private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aRZkAAAAASUVORK5CYII=");

	[SchoolIdentityPostgresFact]
	public async Task Migration_and_real_http_identity_preserve_tenant_data_and_validate_assets_permissions_and_stale_writes()
	{
		await using var factory = new SchoolIdentityPostgresFactory();
		using var first = await NewSchoolAsync(factory);
		using var second = await NewSchoolAsync(factory);
		(await first.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.OK);
		var initial = new SchoolIdentityUpdateDto { Choices = new() { Logo = new() { Mode = SchoolIdentityAssetMode.Hidden } } };
		var initialRace = await Task.WhenAll(second.PutAsJsonAsync(Url, initial), second.PutAsJsonAsync(Url, initial));
		initialRace.Count(result => result.StatusCode == HttpStatusCode.OK).ShouldBe(1);
		initialRace.Count(result => result.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
		var logo = await UploadAsync(first, SchoolIdentityAssetKind.Logo, "logo.png", Png);
		var signature = await UploadAsync(first, SchoolIdentityAssetKind.Signature, "signature.png", Png);
		var choices = new SchoolIdentityChoicesDto { Logo = Custom(logo.Id), Signature = Custom(signature.Id) with { PrincipalName = "Synthetic Principal", Designation = "Principal" } };
		var savedResponse = await first.PutAsJsonAsync(Url, new SchoolIdentityUpdateDto { Choices = choices });
		savedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await savedResponse.Content.ReadAsStringAsync());
		var saved = (await savedResponse.Content.ReadFromJsonAsync<SchoolIdentityProfileDto>())!;
		saved.Effective["logo"].Asset!.Id.ShouldBe(logo.Id);
		(await first.GetByteArrayAsync(Url + $"/assets/{logo.Id}")).ShouldBe(Png);
		(await second.GetAsync(Url + $"/assets/{logo.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await first.GetAsync(Url + "/branding/signature")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		var branding = (await first.GetFromJsonAsync<SchoolBrandingDto>(Url + "/branding"))!;
		branding.Logo!.Id.ShouldBe(logo.Id); branding.Banner.ShouldBeNull();
		first.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, SchoolIdentityPermissions.View);
		var restricted = (await first.GetFromJsonAsync<SchoolIdentityProfileDto>(Url))!;
		restricted.CanManage.ShouldBeFalse();
		restricted.Choices.Signature.ShouldBeNull(); restricted.Effective.ContainsKey("signature").ShouldBeFalse();
		(await first.GetAsync(Url + $"/assets/{signature.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await first.PutAsJsonAsync(Url, new SchoolIdentityUpdateDto { ConcurrencyStamp = saved.ConcurrencyStamp })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		first.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		first.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			SchoolIdentityPermissions.View + "," + SchoolIdentityPermissions.ManageDefaults);
		var editWithoutSignature = await first.PutAsJsonAsync(Url, new SchoolIdentityUpdateDto
		{
			ConcurrencyStamp = saved.ConcurrencyStamp,
			Choices = new() { Logo = Custom(logo.Id), Banner = new() { Mode = SchoolIdentityAssetMode.Hidden }, Signature = null }
		});
		editWithoutSignature.StatusCode.ShouldBe(HttpStatusCode.OK);
		var redactedSave = (await editWithoutSignature.Content.ReadFromJsonAsync<SchoolIdentityProfileDto>())!;
		redactedSave.CanManage.ShouldBeTrue(); redactedSave.Choices.Signature.ShouldBeNull();
		first.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		saved = (await first.GetFromJsonAsync<SchoolIdentityProfileDto>(Url))!;
		saved.Choices.Signature!.AssetId.ShouldBe(signature.Id);
		var firstWrite = new SchoolIdentityUpdateDto { ConcurrencyStamp = saved.ConcurrencyStamp, Choices = choices };
		var response = await first.PutAsJsonAsync(Url, firstWrite); response.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await first.PutAsJsonAsync(Url, firstWrite)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var current = (await response.Content.ReadFromJsonAsync<SchoolIdentityProfileDto>())!;
		var simultaneous = new SchoolIdentityUpdateDto { ConcurrencyStamp = current.ConcurrencyStamp,
			Choices = choices with { Banner = new() { Mode = SchoolIdentityAssetMode.Hidden } } };
		var racing = await Task.WhenAll(first.PutAsJsonAsync(Url, simultaneous), first.PutAsJsonAsync(Url, simultaneous));
		racing.Count(result => result.StatusCode == HttpStatusCode.OK).ShouldBe(1);
		racing.Count(result => result.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
		var preview = await first.PostAsJsonAsync(Url + "/preview", new SchoolIdentityPreviewDto());
		preview.StatusCode.ShouldBe(HttpStatusCode.OK);
		var previewBytes = await preview.Content.ReadAsByteArrayAsync(); previewBytes.Take(4).ShouldBe("%PDF"u8.ToArray());
		var previewPath = System.IO.Path.Combine(AppContext.BaseDirectory, "school-identity-preview.pdf");
		await System.IO.File.WriteAllBytesAsync(previewPath, previewBytes);
		using var invalid = new MultipartFormDataContent(); invalid.Add(new StringContent("0"), "kind"); invalid.Add(new ByteArrayContent("<svg/>"u8.ToArray()), "file", "fake.png");
		(await first.PostAsync(Url + "/assets", invalid)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		using var anonymous = factory.CreateClient(); anonymous.DefaultRequestHeaders.Add("X-Skip-Auth", "true");
		(await anonymous.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		await using var sql = new NpgsqlConnection(factory.ConnectionString); await sql.OpenAsync();
		foreach (var table in new[] { "AppAcademicTerms", "AppStudentManagementSettings", "AppStudentRollNumberAssignments" })
		{
			await using var retained = new NpgsqlCommand($"SELECT count(*) FROM \"{table}\" WHERE \"Sentinel\" = 'preserve'", sql);
			((long)(await retained.ExecuteScalarAsync())!).ShouldBe(1);
		}
		await using var history = new NpgsqlCommand("SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20261014053153_AddSchoolIdentity'", sql);
		((long)(await history.ExecuteScalarAsync())!).ShouldBe(1);
		await sql.CloseAsync();
		await factory.VerifyMigrationDownAsync();
	}

	[SchoolIdentityPostgresFact]
	public async Task Branding_named_routes_revalidate_private_images_after_authentication_and_tenant_resolution()
	{
		await using var factory = new SchoolIdentityPostgresFactory();
		using var first = await NewSchoolAsync(factory);
		using var second = await NewSchoolAsync(factory);
		(await first.GetAsync(Url + "/branding/logo")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
		(await first.GetAsync(Url + "/branding/banner")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
		foreach (var suffix in new[] { "0", "1", "2", "signature", "invalid" })
			(await first.GetAsync(Url + "/branding/" + suffix)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

		var logo = await UploadAsync(first, SchoolIdentityAssetKind.Logo, "logo.png", Png);
		var banner = await UploadAsync(first, SchoolIdentityAssetKind.Banner, "banner.png", Png);
		var savedResponse = await first.PutAsJsonAsync(Url, new SchoolIdentityUpdateDto
		{
			Choices = new() { Logo = Custom(logo.Id), Banner = Custom(banner.Id) }
		});
		savedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await savedResponse.Content.ReadAsStringAsync());
		var saved = (await savedResponse.Content.ReadFromJsonAsync<SchoolIdentityProfileDto>())!;

		// Branding remains available to school users without configuration grants.
		first.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "Unrelated.Permission");
		var metadataResponse = await first.GetAsync(Url + "/branding");
		metadataResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		metadataResponse.Headers.CacheControl!.NoStore.ShouldBeTrue();
		var metadata = JsonDocument.Parse(await metadataResponse.Content.ReadAsStringAsync()).RootElement;
		metadata.EnumerateObject().Select(x => x.Name).OrderBy(x => x).ShouldBe(new[] { "banner", "logo" });
		metadata.GetProperty("logo").EnumerateObject().Select(x => x.Name).OrderBy(x => x)
			.ShouldBe(new[] { "contentType", "height", "id", "width" });
		var image = await first.GetAsync(Url + "/branding/logo");
		image.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await image.Content.ReadAsByteArrayAsync()).ShouldBe(Png);
		image.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
		image.Headers.CacheControl!.Private.ShouldBeTrue();
		image.Headers.CacheControl.NoCache.ShouldBeTrue();
		image.Headers.CacheControl.NoStore.ShouldBeFalse();
		image.Headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
		image.Headers.Vary.ShouldContain("Authorization");
		image.Headers.Vary.ShouldContain("__tenant");
		var tag = image.Headers.ETag!.ToString();
		var bannerImage = await first.GetAsync(Url + "/branding/banner");
		(await bannerImage.Content.ReadAsByteArrayAsync()).ShouldBe(Png);
		bannerImage.Headers.ETag!.ToString().ShouldNotBe(tag);
		foreach (var condition in new[] { tag, "W/" + tag, "\"unrelated\", " + tag, "*" })
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, Url + "/branding/logo");
			request.Headers.TryAddWithoutValidation("If-None-Match", condition);
			var unchanged = await first.SendAsync(request);
			unchanged.StatusCode.ShouldBe(HttpStatusCode.NotModified);
			(await unchanged.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
			unchanged.Headers.ETag!.ToString().ShouldBe(tag);
		}

		using var anonymous = factory.CreateClient();
		anonymous.DefaultRequestHeaders.Add("X-Skip-Auth", "true");
		anonymous.DefaultRequestHeaders.Add("If-None-Match", tag);
		(await anonymous.GetAsync(Url + "/branding/logo")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		second.DefaultRequestHeaders.Add("If-None-Match", tag);
		(await second.GetAsync(Url + "/branding/logo")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
		var otherLogo = await UploadAsync(second, SchoolIdentityAssetKind.Logo, "other.png", Png);
		(await second.PutAsJsonAsync(Url, new SchoolIdentityUpdateDto { Choices = new() { Logo = Custom(otherLogo.Id) } }))
			.StatusCode.ShouldBe(HttpStatusCode.OK);
		var other = await second.GetAsync(Url + "/branding/logo");
		other.StatusCode.ShouldBe(HttpStatusCode.OK);
		other.Headers.ETag!.ToString().ShouldNotBe(tag);

		first.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		var replacement = await UploadAsync(first, SchoolIdentityAssetKind.Logo, "replacement.png", Png);
		var replaced = await first.PutAsJsonAsync(Url, new SchoolIdentityUpdateDto
		{
			ConcurrencyStamp = saved.ConcurrencyStamp, Choices = new() { Logo = Custom(replacement.Id) }
		});
		replaced.StatusCode.ShouldBe(HttpStatusCode.OK);
		first.DefaultRequestHeaders.Add("If-None-Match", tag);
		var changed = await first.GetAsync(Url + "/branding/logo");
		changed.StatusCode.ShouldBe(HttpStatusCode.OK);
		changed.Headers.ETag!.ToString().ShouldNotBe(tag);
		var replacedProfile = (await replaced.Content.ReadFromJsonAsync<SchoolIdentityProfileDto>())!;
		(await first.PutAsJsonAsync(Url, new SchoolIdentityUpdateDto
		{
			ConcurrencyStamp = replacedProfile.ConcurrencyStamp,
			Choices = new() { Logo = new() { Mode = SchoolIdentityAssetMode.Hidden } }
		})).StatusCode.ShouldBe(HttpStatusCode.OK);
		var hidden = await first.GetAsync(Url + "/branding/logo");
		hidden.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		hidden.Headers.CacheControl!.NoStore.ShouldBeTrue();
	}

	private static SchoolIdentitySelectionDto Custom(Guid id) => new() { Mode = SchoolIdentityAssetMode.Custom, AssetId = id };
	private static async Task<SchoolIdentityAssetDto> UploadAsync(HttpClient client, SchoolIdentityAssetKind kind, string name, byte[] bytes)
	{
		using var form = new MultipartFormDataContent(); form.Add(new StringContent(((int)kind).ToString()), "kind"); form.Add(new ByteArrayContent(bytes), "file", name);
		var response = await client.PostAsync(Url + "/assets", form);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<SchoolIdentityAssetDto>())!;
	}

	private static async Task<HttpClient> NewSchoolAsync(SchoolIdentityPostgresFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>(); var units = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var hostId = Guid.NewGuid();
		using (tenant.Change(null)) using (var unit = units.Begin(requiresNew: true, isTransactional: true))
		{
			await scope.ServiceProvider.GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(hostId, $"host{hostId:N}", "host@example.test"));
			await unit.CompleteAsync();
		}
		using var host = factory.CreateClient(); host.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, "host"); host.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, hostId.ToString());
		var response = await host.PostAsJsonAsync("/api/host/tenants", new CreateHostTenantInput { Name = "identity" + Guid.NewGuid().ToString("N"),
			InstituteName = "Synthetic Identity School", Email = "office@example.test", ContactNumber = "9876543210", Address1 = "Test Road", City = "Surat", State = "Gujarat",
			Country = "India", PostalCode = "395001", AdminEmailAddress = "admin@example.test", AdminPassword = "Synthetic-Identity-29!" });
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var school = (await response.Content.ReadFromJsonAsync<HostTenantDto>())!; Guid user;
		using (tenant.Change(school.Id)) using (var unit = units.Begin(requiresNew: true))
		{
			user = (await scope.ServiceProvider.GetRequiredService<IIdentityUserRepository>().FindByNormalizedUserNameAsync("ADMIN"))!.Id;
			await unit.CompleteAsync();
		}
		var client = factory.CreateClient(); client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, school.Id.ToString());
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, user.ToString()); client.DefaultRequestHeaders.Add("__tenant", school.Id.ToString()); return client;
	}
}
