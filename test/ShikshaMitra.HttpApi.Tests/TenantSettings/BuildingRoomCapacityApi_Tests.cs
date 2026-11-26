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
		var initial = Update(input, 10);
		initial.BuildingName = "Transport block";
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
		saved.Floors[0].TotalRoom.ShouldBe(10);
		saved.Floors[0].RoomCount.ShouldBe(2);
	}

	[Theory]
	[InlineData("name")]
	[InlineData("number")]
	[InlineData("capacity")]
	[InlineData("delete")]
	public async Task Occupied_floor_rejects_changes_and_removal_without_saving_building_details(string change)
	{
		var (client, input) = await ClientAsync();
		using var owner = client;
		await CreateTwoRoomsAsync(owner, input);
		var update = Update(input, 10);
		var floor = update.FloorsToUpdate[0];
		if (change == "name") floor.FloorName = "Renamed";
		if (change == "number") floor.FloorNo = 1;
		if (change == "capacity") floor.TotalRoom = 11;
		if (change == "delete")
		{
			update.FloorsToUpdate.Clear();
			update.FloorIdsToDelete.Add(input.FloorId);
			update.FloorsToAdd.Add(new() { FloorName = "Replacement", FloorNo = 0, TotalRoom = 10 });
		}
		var response = await owner.PutAsJsonAsync($"{BuildingsUrl}/{input.BuildingId}", update, Json);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		error.RootElement.GetProperty("errorCode").GetString().ShouldBe("ShikshaMitra.TenantSettings:FloorInUse");
		var saved = (await owner.GetFromJsonAsync<BuildingDto>($"{BuildingsUrl}/{input.BuildingId}", Json))!;
		saved.BuildingName.ShouldNotBe(update.BuildingName);
		saved.Floors[0].FloorName.ShouldBe("Ground");
		saved.Floors[0].FloorNo.ShouldBe(0);
		saved.Floors[0].TotalRoom.ShouldBe(10);
		saved.Floors[0].RoomCount.ShouldBe(2);
	}

	[Fact]
	public async Task First_floor_can_be_edited_without_rooms_but_cannot_be_replaced()
	{
		var (client, input) = await ClientAsync();
		using var owner = client;
		var update = Update(input, 4);
		update.FloorsToUpdate[0].FloorName = "Renamed";
		update.FloorsToUpdate[0].FloorNo = 1;
		var url = $"{BuildingsUrl}/{input.BuildingId}";
		(await owner.PutAsJsonAsync(url, update, Json)).StatusCode.ShouldBe(HttpStatusCode.OK);
		update.FloorsToUpdate.Clear();
		update.FloorIdsToDelete.Add(input.FloorId);
		update.FloorsToAdd.Add(new() { FloorName = "Replacement", FloorNo = 1, TotalRoom = 4 });
		var response = await owner.PutAsJsonAsync(url, update, Json);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		error.RootElement.GetProperty("errorCode").GetString().ShouldBe("ShikshaMitra.TenantSettings:FirstFloorCannotBeRemoved");
		var saved = (await owner.GetFromJsonAsync<BuildingDto>(url, Json))!;
		saved.Floors[0].Id.ShouldBe(input.FloorId);
		saved.Floors[0].FloorName.ShouldBe("Renamed");
	}

	[Fact]
	public async Task Removing_the_last_room_unlocks_the_floor()
	{
		var (client, input) = await ClientAsync();
		using var owner = client;
		var created = await owner.PostAsJsonAsync(RoomsUrl, input, Json);
		created.StatusCode.ShouldBe(HttpStatusCode.Created);
		var room = (await created.Content.ReadFromJsonAsync<RoomDto>(Json))!;
		(await owner.DeleteAsync($"{RoomsUrl}/{room.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
		var response = await owner.PutAsJsonAsync($"{BuildingsUrl}/{input.BuildingId}", Update(input, 1), Json);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var saved = (await response.Content.ReadFromJsonAsync<BuildingDto>(Json))!;
		saved.Floors[0].TotalRoom.ShouldBe(1);
		saved.Floors[0].RoomCount.ShouldBe(0);
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
		FloorsToUpdate = [new() { Id = input.FloorId, FloorName = "Ground", FloorNo = 0, TotalRoom = limit }]
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
