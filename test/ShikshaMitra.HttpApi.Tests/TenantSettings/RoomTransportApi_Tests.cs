using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

[Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public class RoomTransportApi_Tests(RoomTransportTestFactory factory) : IClassFixture<RoomTransportTestFactory>
{
	private const string Url = "/api/institute-setup/rooms";
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
	private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aRZkAAAAASUVORK5CYII=");
	private static readonly byte[] Jpg = [255, 216, 255, 224, 0, 0, 0, 0, 0, 0, 0, 0];

	[Fact]
	public async Task Column_filters_combine_OR_within_a_column_AND_across_columns_before_count_and_paging()
	{
		var (client, input) = await ClientAsync();
		using var owner = client;
		foreach (var (name, rows) in new[] { ("Alpha", 2), ("Beta", 4), ("Gamma", 6) })
		{
			input.RoomName = name;
			input.ColumnRows = [rows];
			(await owner.PostAsJsonAsync(Url, input, Json)).StatusCode.ShouldBe(HttpStatusCode.Created);
		}
		var filters = Uri.EscapeDataString("[{\"column\":\"roomName\",\"type\":\"startsWith\",\"value\":\"AL\"},{\"column\":\"roomName\",\"type\":\"equals\",\"value\":\"gamma\"},{\"column\":\"capacity\",\"type\":\"between\",\"value\":{\"min\":6,\"max\":15}}]");
		var result = (await owner.GetFromJsonAsync<Volo.Abp.Application.Dtos.PagedResultDto<RoomDto>>(
			$"{Url}?columnFilters={filters}&maxResultCount=1&sorting=roomName%20asc", Json))!;
		result.TotalCount.ShouldBe(1);
		result.Items.Single().RoomName.ShouldBe("Gamma");
		var malformed = Uri.EscapeDataString("[{\"column\":\"roomName\",\"type\":\"execute\",\"value\":\"x\"}]");
		(await owner.GetAsync($"{Url}?columnFilters={malformed}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Feature_filters_match_the_case_insensitive_displayed_comma_joined_names()
	{
		var (client, input) = await ClientAsync();
		using var owner = client;
		(await owner.PostAsJsonAsync(Url + "/features", new { name = "Projector" }, Json)).EnsureSuccessStatusCode();
		(await owner.PostAsJsonAsync(Url + "/features", new { name = "Air conditioner" }, Json)).EnsureSuccessStatusCode();
		input.Features = ["Projector", "Air conditioner"];
		(await owner.PostAsJsonAsync(Url, input, Json)).StatusCode.ShouldBe(HttpStatusCode.Created);
		input.RoomName = "Other";
		input.Features = [];
		(await owner.PostAsJsonAsync(Url, input, Json)).StatusCode.ShouldBe(HttpStatusCode.Created);
		var filters = Uri.EscapeDataString("[{\"column\":\"features\",\"type\":\"contains\",\"value\":\"TOR, AIR\"}]");
		var result = (await owner.GetFromJsonAsync<Volo.Abp.Application.Dtos.PagedResultDto<RoomDto>>(
			$"{Url}?columnFilters={filters}&maxResultCount=1", Json))!;
		result.TotalCount.ShouldBe(1);
		result.Items.Single().RoomName.ShouldBe("Transport room");
	}

	[Fact]
	public async Task Multipart_room_and_image_save_together_and_binary_reads_preserve_content_type()
	{
		var (client, input) = await ClientAsync();
		using var owner = client;
		using var create = Form(input, Png);
		var response = await owner.PostAsync(Url, create);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		var room = (await response.Content.ReadFromJsonAsync<RoomDto>(Json))!;
		room.HasImage.ShouldBeTrue();
		var image = await owner.GetAsync($"{Url}/{room.Id}/image/file");
		image.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await image.Content.ReadAsByteArrayAsync()).ShouldBe(Png);
		image.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
		image.Headers.CacheControl!.NoStore.ShouldBeTrue();
		image.Headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
		(await owner.GetFromJsonAsync<RoomImageDto>($"{Url}/{room.Id}/image", Json))!.Content.ShouldBe(Png);
		input.RoomName = "Renamed room";
		using var preserve = Form(input);
		(await owner.PutAsync($"{Url}/{room.Id}", preserve)).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await owner.GetByteArrayAsync($"{Url}/{room.Id}/image/file")).ShouldBe(Png);
		using var replace = Form(input, Jpg, "room.jpg");
		(await owner.PutAsync($"{Url}/{room.Id}", replace)).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await owner.GetAsync($"{Url}/{room.Id}/image/file")).Content.Headers.ContentType!.MediaType.ShouldBe("image/jpeg");
		input.RemoveImage = true;
		using var remove = Form(input);
		(await owner.PutAsync($"{Url}/{room.Id}", remove)).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await owner.GetAsync($"{Url}/{room.Id}/image/file")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
		input.RemoveImage = false;
		(await owner.PostAsJsonAsync(Url, input, Json)).StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Fact]
	public async Task Failed_room_update_rolls_back_the_new_blob_and_keeps_the_existing_image()
	{
		var (client, input) = await ClientAsync();
		using var owner = client;
		using var create = Form(input, Png);
		var saved = await owner.PostAsync(Url, create);
		saved.StatusCode.ShouldBe(HttpStatusCode.Created, await saved.Content.ReadAsStringAsync());
		var room = (await saved.Content.ReadFromJsonAsync<RoomDto>(Json))!;
		input.RoomName = "rollback-failure";
		using var update = Form(input, Jpg, "room.jpg");
		(await owner.PutAsync($"{Url}/{room.Id}", update)).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
		(await owner.GetFromJsonAsync<RoomDto>($"{Url}/{room.Id}", Json))!.RoomName.ShouldBe("Transport room");
		(await owner.GetByteArrayAsync($"{Url}/{room.Id}/image/file")).ShouldBe(Png);
		(await factory.BlobCountAsync(input.BuildingId)).ShouldBe(1);
	}

	[Theory]
	[InlineData("malformed")]
	[InlineData("signature")]
	[InlineData("oversize")]
	[InlineData("embedded-image")]
	public async Task Invalid_multipart_input_is_rejected_without_persisting_a_room(string failure)
	{
		var (client, input) = await ClientAsync();
		using var owner = client;
		using var form = failure is "malformed" or "embedded-image" ? new MultipartFormDataContent() : Form(input,
			failure == "oversize" ? new byte[5 * 1024 * 1024 + 1] : failure == "signature" ? [1, 2, 3, 4] : null);
		if (failure == "malformed") form.Add(new StringContent("{"), "input");
		if (failure == "embedded-image")
		{
			input.Image = new RoomImageInput { FileName = "room.png", Content = Png };
			form.Add(new StringContent(JsonSerializer.Serialize(input, Json)), "input");
		}
		(await owner.PostAsync(Url, form)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await owner.GetFromJsonAsync<Volo.Abp.Application.Dtos.PagedResultDto<RoomDto>>(Url, Json))!.TotalCount.ShouldBe(0);
	}

	[Fact]
	public async Task Binary_image_route_enforces_authentication_permissions_and_tenant_ownership()
	{
		var (client, input) = await ClientAsync();
		using var owner = client;
		using var create = Form(input, Png);
		var saved = await owner.PostAsync(Url, create);
		saved.StatusCode.ShouldBe(HttpStatusCode.Created, await saved.Content.ReadAsStringAsync());
		var room = (await saved.Content.ReadFromJsonAsync<RoomDto>(Json))!;
		var (other, _) = await ClientAsync();
		using var foreign = other;
		(await foreign.GetAsync($"{Url}/{room.Id}/image/file")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		using var anonymous = factory.CreateClient();
		anonymous.DefaultRequestHeaders.Add("X-Skip-Auth", "true");
		(await anonymous.GetAsync($"{Url}/{room.Id}/image/file")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		owner.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.InstituteSetup.RoomManagement.Add);
		(await owner.GetAsync($"{Url}/{room.Id}/image/file")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	private async Task<(HttpClient Client, CreateUpdateRoomDto Input)> ClientAsync()
	{
		var tenant = Guid.NewGuid();
		var input = await factory.SeedAsync(tenant);
		var client = factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenant.ToString());
		return (client, input);
	}

	private static MultipartFormDataContent Form(CreateUpdateRoomDto input, byte[]? bytes = null, string name = "room.png")
	{
		var form = new MultipartFormDataContent();
		form.Add(new StringContent(JsonSerializer.Serialize(input, Json)), "input");
		if (bytes != null)
		{
			var image = new ByteArrayContent(bytes);
			image.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
			form.Add(image, "image", name);
		}
		return form;
	}
}

public class RoomTransportTestFactory : ShikshaMitraApiTestFactory
{
	protected override bool InitializeTestData => false;
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureServices(services => services.Configure<AbpDbContextOptions>(options => options.PreConfigure(context =>
			context.DbContextOptions.AddInterceptors(new RoomSaveFailureInterceptor()))));
	}

	public async Task<CreateUpdateRoomDto> SeedAsync(Guid tenantId)
	{
		using var scope = Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		using (currentTenant.Change(null))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var tenant = await scope.ServiceProvider.GetRequiredService<ITenantManager>().CreateAsync("rooms-" + tenantId);
			typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(tenant, tenantId);
			await scope.ServiceProvider.GetRequiredService<IRepository<Tenant, Guid>>().InsertAsync(tenant);
			await unit.CompleteAsync();
		}
		using (currentTenant.Change(tenantId))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var building = await scope.ServiceProvider.GetRequiredService<IRepository<Building, Guid>>().InsertAsync(
				new Building { BuildingName = "Transport block", TotalFloors = 1 }, true);
			var floor = await scope.ServiceProvider.GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(
				new Floor { BuildingId = building.Id, FloorName = "Ground", FloorNo = 0, TotalRoom = 10 }, true);
			var type = await scope.ServiceProvider.GetRequiredService<IRepository<RoomType, Guid>>().InsertAsync(
				new RoomType { RoomTypeName = "Classroom", ShortName = "CR" }, true);
			await unit.CompleteAsync();
			return new() { RoomName = "Transport room", BuildingId = building.Id, FloorId = floor.Id,
				RoomTypeId = type.Id, Capacity = 4, BenchCapacity = 2, ColumnRows = [2] };
		}
	}

	public async Task<long> BlobCountAsync(Guid buildingId)
	{
		using var scope = Services.CreateScope();
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		var tenantId = await db.Set<Building>().IgnoreQueryFilters().Where(x => x.Id == buildingId).Select(x => x.TenantId).SingleAsync();
		return await db.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM \"AbpBlobs\" WHERE \"TenantId\" = {0}", tenantId!).SingleAsync();
	}
}

public class RoomSaveFailureInterceptor : SaveChangesInterceptor
{
	public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
		int result, CancellationToken cancellationToken = default)
	{
		if (eventData.Context?.ChangeTracker.Entries<Room>().Any(entry =>
			entry.Entity.RoomName == "rollback-failure"
			&& entry.Entity.ExtraProperties.TryGetValue("RoomImageContentType", out var type)
			&& type?.ToString() == "image/jpeg") == true)
			throw new InvalidOperationException("Synthetic room persistence failure.");
		return base.SavedChangesAsync(eventData, result, cancellationToken);
	}
}
