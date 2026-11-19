using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class RoomFloorCapacity_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Creating_a_room_on_a_full_floor_returns_the_limit_and_preserves_the_existing_rooms()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var input = await CreateInputAsync();
		var rooms = GetRequiredService<IRoomAppService>();
		await rooms.CreateAsync(input);
		input.RoomName = "Second room";
		var error = await Should.ThrowAsync<BusinessException>(() => rooms.CreateAsync(input));
		error.Code.ShouldBe("ShikshaMitra.TenantSettings:RoomFloorLimitReached");
		error.Data["BuildingId"].ShouldBe(input.BuildingId);
		error.Data["FloorId"].ShouldBe(input.FloorId);
		error.Data["RoomLimit"].ShouldBe(1);
		error.Data["RoomCount"].ShouldBe(1);
		(await rooms.GetListAsync(new() { FloorId = input.FloorId })).TotalCount.ShouldBe(1);
	}

	[Fact]
	public async Task Editing_a_room_on_its_full_floor_keeps_its_place()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var input = await CreateInputAsync();
		var rooms = GetRequiredService<IRoomAppService>();
		var room = await rooms.CreateAsync(input);
		input.RoomName = "Renamed room";
		(await rooms.UpdateAsync(room.Id, input)).RoomName.ShouldBe("Renamed room");
	}

	[Fact]
	public async Task Moving_a_room_to_a_full_floor_is_rejected_without_changing_its_floor()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var destination = await CreateInputAsync();
		var source = await CreateInputAsync();
		var rooms = GetRequiredService<IRoomAppService>();
		await rooms.CreateAsync(destination);
		var room = await rooms.CreateAsync(source);
		source.BuildingId = destination.BuildingId;
		source.FloorId = destination.FloorId;
		(await Should.ThrowAsync<BusinessException>(() => rooms.UpdateAsync(room.Id, source)))
			.Code.ShouldBe("ShikshaMitra.TenantSettings:RoomFloorLimitReached");
		(await rooms.GetAsync(room.Id)).FloorId.ShouldBe(room.FloorId);
	}

	[Fact]
	public async Task Increasing_a_floor_limit_allows_the_next_room_and_returns_current_counts()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var input = await CreateInputAsync();
		var rooms = GetRequiredService<IRoomAppService>();
		await rooms.CreateAsync(input);
		var buildings = GetRequiredService<IBuildingAppService>();
		await buildings.UpdateAsync(input.BuildingId, Update(input, 2));
		input.RoomName = "Second room";
		await rooms.CreateAsync(input);
		var floor = (await buildings.GetAsync(input.BuildingId)).Floors.Single();
		floor.TotalRoom.ShouldBe(2);
		floor.RoomCount.ShouldBe(2);
		var listed = (await buildings.GetListAsync()).Single(building => building.Id == input.BuildingId).Floors.Single();
		listed.RoomCount.ShouldBe(2);
	}

	[Fact]
	public async Task Lowering_a_floor_limit_below_its_room_count_is_rejected()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var input = await CreateInputAsync(2);
		var rooms = GetRequiredService<IRoomAppService>();
		await rooms.CreateAsync(input);
		input.RoomName = "Second room";
		await rooms.CreateAsync(input);
		var buildings = GetRequiredService<IBuildingAppService>();
		(await Should.ThrowAsync<BusinessException>(() => buildings.UpdateAsync(input.BuildingId, Update(input, 1))))
			.Code.ShouldBe("ShikshaMitra.TenantSettings:FloorRoomLimitBelowUsage");
		(await buildings.GetAsync(input.BuildingId)).Floors.Single().TotalRoom.ShouldBe(2);
	}

	[Fact]
	public async Task Another_tenant_cannot_add_a_room_to_the_selected_building_or_read_its_counts()
	{
		var tenants = GetRequiredService<ICurrentTenant>();
		using var tenant = tenants.Change(Guid.NewGuid());
		var input = await CreateInputAsync();
		using var otherTenant = tenants.Change(Guid.NewGuid());
		await Should.ThrowAsync<EntityNotFoundException>(() => GetRequiredService<IRoomAppService>().CreateAsync(input));
		(await GetRequiredService<IRoomAppService>().GetListAsync(new())).TotalCount.ShouldBe(0);
		(await GetRequiredService<IBuildingAppService>().GetListAsync()).ShouldBeEmpty();
	}

	[Fact]
	public async Task Another_tenant_cannot_delete_the_building_or_its_floors()
	{
		var tenants = GetRequiredService<ICurrentTenant>();
		using var tenant = tenants.Change(Guid.NewGuid());
		var input = await CreateInputAsync();
		var buildings = GetRequiredService<IBuildingAppService>();
		using (tenants.Change(Guid.NewGuid()))
			await Should.ThrowAsync<EntityNotFoundException>(() => buildings.DeleteAsync(input.BuildingId));
		(await buildings.GetAsync(input.BuildingId)).Floors.Single().Id.ShouldBe(input.FloorId);
	}

	private Task<CreateUpdateRoomDto> CreateInputAsync(int limit = 1) => WithUnitOfWorkAsync(async () =>
	{
		var building = await GetRequiredService<IRepository<Building, Guid>>()
			.InsertAsync(new Building { BuildingName = "Capacity test", TotalFloors = 1 }, autoSave: true);
		var floor = await GetRequiredService<IRepository<Floor, Guid>>()
			.InsertAsync(new Floor { FloorName = "Ground floor", BuildingId = building.Id, FloorNo = 0, TotalRoom = limit }, autoSave: true);
		var type = await GetRequiredService<IRepository<RoomType, Guid>>()
			.InsertAsync(new RoomType { RoomTypeName = "Classroom", ShortName = "CR" }, autoSave: true);
		return new CreateUpdateRoomDto
		{
			RoomName = "First room", BuildingId = building.Id, FloorId = floor.Id, RoomTypeId = type.Id,
			ColumnRows = [2], BenchCapacity = 2, Capacity = 4
		};
	});

	private static UpdateBuildingDto Update(CreateUpdateRoomDto room, int limit) => new()
	{
		BuildingName = "Capacity test", TotalFloors = 1,
		FloorsToUpdate = [new() { Id = room.FloorId, FloorName = "Ground floor", FloorNo = 0, TotalRoom = limit }]
	};
}
