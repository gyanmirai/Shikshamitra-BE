using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Shouldly;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Xunit;

namespace ShikshaMitra.TenantSettings;

[Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public class BuildingRoomCapacityApi_Tests(BuildingRoomCapacityTestFactory factory) : IClassFixture<BuildingRoomCapacityTestFactory>
{
	private const string BuildingsUrl = "/api/institute-setup/buildings";
	private const string RoomsUrl = "/api/institute-setup/rooms";
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

	[Fact]
	public async Task Reducing_capacity_below_saved_rooms_returns_actionable_context_without_development_details_and_keeps_saved_values()
	{
		var (client, input) = await ClientAsync();
		using var owner = client;
		await CreateTwoRoomsAsync(owner, input);
		var initial = Update(input, 2);
		initial.BuildingName = "Transport block";
		initial.FloorsToUpdate[0].FloorName = "Ground";
		(await owner.PutAsJsonAsync($"{BuildingsUrl}/{input.BuildingId}", initial, Json)).StatusCode.ShouldBe(HttpStatusCode.OK);
		var response = await owner.PutAsJsonAsync($"{BuildingsUrl}/{input.BuildingId}", Update(input, 1), Json);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		var error = payload.RootElement;
		error.GetProperty("errorCode").GetString().ShouldBe("ShikshaMitra.TenantSettings:FloorRoomLimitBelowUsage");
		error.GetProperty("detail").GetString().ShouldBe("Cannot set total rooms to 1. Ground already has 2 rooms. Delete or reassign existing rooms before reducing the total room capacity.");
		var data = error.GetProperty("data");
		data.GetProperty("FloorId").GetString().ShouldBe(input.FloorId.ToString());
		data.GetProperty("RoomLimit").GetInt32().ShouldBe(1);
		data.GetProperty("RoomCount").GetInt32().ShouldBe(2);
		var saved = (await owner.GetFromJsonAsync<BuildingDto>($"{BuildingsUrl}/{input.BuildingId}", Json))!;
		saved.BuildingName.ShouldBe("Transport block");
		saved.Floors[0].FloorName.ShouldBe("Ground");
		saved.Floors[0].TotalRoom.ShouldBe(2);
		saved.Floors[0].RoomCount.ShouldBe(2);
	}

	[Theory]
	[InlineData(2)]
	[InlineData(3)]
	public async Task Capacity_equal_to_or_above_saved_room_count_can_be_saved(int limit)
	{
		var (client, input) = await ClientAsync();
		using var owner = client;
		await CreateTwoRoomsAsync(owner, input);
		var response = await owner.PutAsJsonAsync($"{BuildingsUrl}/{input.BuildingId}", Update(input, limit), Json);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var saved = (await response.Content.ReadFromJsonAsync<BuildingDto>(Json))!;
		saved.Floors[0].TotalRoom.ShouldBe(limit);
		saved.Floors[0].RoomCount.ShouldBe(2);
	}

	[Fact]
	public async Task Deleting_a_room_releases_capacity_for_a_floor_reduction()
	{
		var (client, input) = await ClientAsync();
		using var owner = client;
		var room = await CreateTwoRoomsAsync(owner, input);
		(await owner.DeleteAsync($"{RoomsUrl}/{room.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
		var response = await owner.PutAsJsonAsync($"{BuildingsUrl}/{input.BuildingId}", Update(input, 1), Json);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var saved = (await response.Content.ReadFromJsonAsync<BuildingDto>(Json))!;
		saved.Floors[0].TotalRoom.ShouldBe(1);
		saved.Floors[0].RoomCount.ShouldBe(1);
	}

	private async Task<(HttpClient Client, CreateUpdateRoomDto Input)> ClientAsync()
	{
		var tenantId = Guid.NewGuid();
		var input = await factory.SeedAsync(tenantId);
		var client = factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenantId.ToString());
		return (client, input);
	}

	private static async Task<RoomDto> CreateTwoRoomsAsync(HttpClient client, CreateUpdateRoomDto input)
	{
		(await client.PostAsJsonAsync(RoomsUrl, input, Json)).StatusCode.ShouldBe(HttpStatusCode.Created);
		input.RoomName = "Second room";
		var response = await client.PostAsJsonAsync(RoomsUrl, input, Json);
		response.StatusCode.ShouldBe(HttpStatusCode.Created);
		return (await response.Content.ReadFromJsonAsync<RoomDto>(Json))!;
	}

	private static UpdateBuildingDto Update(CreateUpdateRoomDto input, int limit) => new()
	{
		BuildingName = "Changed block", TotalFloors = 1,
		FloorsToUpdate = [new() { Id = input.FloorId, FloorName = "Changed floor", FloorNo = 0, TotalRoom = limit }]
	};
}

public class BuildingRoomCapacityTestFactory : RoomTransportTestFactory
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureServices(services => services.PostConfigure<AbpExceptionHandlingOptions>(options =>
		{
			options.SendExceptionsDetailsToClients = false;
			options.SendStackTraceToClients = false;
		}));
	}
}
