using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Shouldly;
using Xunit;

namespace ShikshaMitra.TenantSettings;

[Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public class RoomZeroLayoutApi_Tests(RoomTransportTestFactory factory) : IClassFixture<RoomTransportTestFactory>
{
	private const string Url = "/api/institute-setup/rooms";
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

	[Theory]
	[InlineData(false, true)]
	[InlineData(false, false)]
	[InlineData(true, true)]
	[InlineData(true, false)]
	public async Task Zero_layout_values_roundtrip_and_restore_through_create_and_update(bool multipart, bool zeroColumns)
	{
		var tenant = Guid.NewGuid();
		var input = await factory.SeedAsync(tenant);
		using var client = factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenant.ToString());
		input.ColumnRows = zeroColumns ? [] : [2, 3];
		input.BenchesPerColumn = zeroColumns ? 0 : 2;
		input.BenchCapacity = zeroColumns ? 2 : 0;
		input.TotalBenches = 0;
		input.Capacity = 0;
		using var createBody = Body(input, multipart);
		var create = await client.PostAsync(Url, createBody);
		create.StatusCode.ShouldBe(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
		var room = (await create.Content.ReadFromJsonAsync<RoomDto>(Json))!;
		var saved = (await client.GetFromJsonAsync<RoomDto>($"{Url}/{room.Id}", Json))!;
		saved.ColumnRows.ShouldBe(input.ColumnRows);
		saved.BenchesPerColumn.ShouldBe(input.BenchesPerColumn);
		saved.BenchesPerRow.ShouldBe(zeroColumns ? 0 : 3);
		saved.BenchCapacity.ShouldBe(input.BenchCapacity);
		saved.TotalBenches.ShouldBe(0);
		saved.Capacity.ShouldBe(0);

		input.ColumnRows = [2, 3];
		input.BenchCapacity = 2;
		input.Capacity = 10;
		using var restoreBody = Body(input, multipart);
		var restore = await client.PutAsync($"{Url}/{room.Id}", restoreBody);
		restore.StatusCode.ShouldBe(HttpStatusCode.OK, await restore.Content.ReadAsStringAsync());
		saved = (await client.GetFromJsonAsync<RoomDto>($"{Url}/{room.Id}", Json))!;
		saved.TotalBenches.ShouldBe(5);
		saved.Capacity.ShouldBe(10);

		input.ColumnRows = zeroColumns ? [] : [2, 3];
		input.BenchCapacity = zeroColumns ? 2 : 0;
		input.Capacity = 0;
		using var zeroBody = Body(input, multipart);
		var update = await client.PutAsync($"{Url}/{room.Id}", zeroBody);
		update.StatusCode.ShouldBe(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
		saved = (await client.GetFromJsonAsync<RoomDto>($"{Url}/{room.Id}", Json))!;
		saved.ColumnRows.ShouldBe(input.ColumnRows);
		saved.TotalBenches.ShouldBe(0);
		saved.Capacity.ShouldBe(0);
	}

	private static HttpContent Body(CreateUpdateRoomDto input, bool multipart)
	{
		if (!multipart) return JsonContent.Create(input, options: Json);
		var body = new MultipartFormDataContent();
		body.Add(new StringContent(JsonSerializer.Serialize(input, Json)), "input");
		return body;
	}
}
