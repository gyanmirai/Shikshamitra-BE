using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;
using Xunit.Abstractions;

namespace ShikshaMitra.TenantSettings;

public class RoomPerformance_Tests(ITestOutputHelper output) : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task List_query_count_stays_bounded_when_the_page_contains_more_distinct_relations()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		for (var index = 0; index < 8; index++)
			await SeedAsync("Room " + index, 10);
		var one = await ReadPageAsync(1);
		var eight = await ReadPageAsync(8);
		output.WriteLine($"Room list SQL commands: one row={one.Queries}; eight rows={eight.Queries}.");
		one.Queries.ShouldBeGreaterThan(0);
		eight.Queries.ShouldBeLessThanOrEqualTo(one.Queries);
		eight.Page.TotalCount.ShouldBe(8);
		eight.Page.Items.Count.ShouldBe(8);
		eight.Page.Items.ShouldAllBe(room => room.BuildingName == "Block " + room.RoomName
			&& room.FloorName == "Floor " + room.RoomName && room.RoomTypeName == "Type " + room.RoomName);
	}

	[Theory]
	[InlineData("buildingName desc")]
	[InlineData("floorName desc")]
	[InlineData("roomTypeName desc")]
	public async Task Related_name_sorting_is_applied_before_paging(string sorting)
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		await SeedAsync("Room A", 10);
		await SeedAsync("Room Z", 10);
		var result = await GetRequiredService<IRoomAppService>().GetListAsync(new GetRoomListDto
			{ Sorting = sorting, MaxResultCount = 1 });
		result.TotalCount.ShouldBe(2);
		result.Items.Single().RoomName.ShouldBe("Room Z");
	}

	[Fact]
	public async Task Full_floor_rejects_new_rooms_and_moves_but_allows_editing_its_existing_room()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var full = await SeedAsync("Full", 1);
		var service = GetRequiredService<IRoomAppService>();
		var input = Input(full);
		var room = await service.CreateAsync(input);
		input.RoomName = "Edited";
		(await service.UpdateAsync(room.Id, input)).RoomName.ShouldBe("Edited");
		(await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(input))).Code
			.ShouldBe("ShikshaMitra.TenantSettings:RoomFloorLimitReached");
		var spare = await SeedAsync("Spare", 2);
		var other = await service.CreateAsync(Input(spare));
		(await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(other.Id, input))).Code
			.ShouldBe("ShikshaMitra.TenantSettings:RoomFloorLimitReached");
		(await service.GetAsync(other.Id)).FloorId.ShouldBe(spare.FloorId);
	}

	[Fact]
	public async Task Building_cannot_reduce_a_floor_limit_below_saved_usage()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var place = await SeedAsync("Capacity", 2);
		var rooms = GetRequiredService<IRoomAppService>();
		await rooms.CreateAsync(Input(place));
		await rooms.CreateAsync(Input(place));
		var service = GetRequiredService<IBuildingAppService>();
		(await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(place.BuildingId, new UpdateBuildingDto
		{
			BuildingName = "Must not persist", TotalFloors = 1,
			FloorsToUpdate = [new() { Id = place.FloorId, FloorName = "Changed", TotalRoom = 1, FloorNo = 0 }]
		}))).Code.ShouldBe("ShikshaMitra.TenantSettings:FloorRoomLimitBelowUsage");
		var restored = await service.GetAsync(place.BuildingId);
		restored.BuildingName.ShouldBe("Block Capacity");
		restored.Floors.Single().TotalRoom.ShouldBe(2);
		restored.Floors.Single().RoomCount.ShouldBe(2);
		(await service.GetListAsync()).Single().Floors.Single().RoomCount.ShouldBe(2);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Assigned_room_deletion_requires_unassigning_the_division_and_keeps_the_room_image(bool historical)
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var place = await SeedAsync("Assigned", 2);
		var input = Input(place);
		input.Image = new RoomImageInput { FileName = "room.png", Content = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0] };
		var service = GetRequiredService<IRoomAppService>();
		var room = await service.CreateAsync(input);
		var divisionId = await WithUnitOfWorkAsync(async () =>
		{
			var year = await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, "Assigned room year", new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, EducationBoardType.CBSE, year.Id), true);
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, "English", board.Id), true);
			var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, "Primary", null, medium.Id), true);
			var standard = await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, "Class 1", 6, stage.Id, null), true);
			var division = new Division(Guid.NewGuid(), tenantId, "A", standard.Id, 20, room.Id.ToString());
			division.AssignRoom(room.Id.ToString(), room.Id);
			division.IsDeleted = historical;
			await GetRequiredService<IDivisionRepository>().InsertAsync(division, true);
			return division.Id;
		});
		(await Should.ThrowAsync<BusinessException>(() => service.DeleteAsync(room.Id))).Code
			.ShouldBe("ShikshaMitra.TenantSettings:RoomInUse");
		(await service.GetImageAsync(room.Id))!.Content.ShouldBe(input.Image.Content);
		(await service.GetAsync(room.Id)).HasImage.ShouldBeTrue();
		if (historical) return;
		await WithUnitOfWorkAsync(async () =>
		{
			var division = await GetRequiredService<IDivisionRepository>().GetAsync(divisionId);
			division.AssignRoom("", null);
		});
		await service.DeleteAsync(room.Id);
	}

	private async Task<(int Queries, Volo.Abp.Application.Dtos.PagedResultDto<RoomDto> Page)> ReadPageAsync(int size) =>
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			using var queries = new AcademicQueryCounter(db.Database.GetDbConnection());
			var page = await GetRequiredService<IRoomAppService>().GetListAsync(new GetRoomListDto
				{ MaxResultCount = size, Sorting = "RoomName asc" });
			return (queries.Count, page);
		});

	private Task<Place> SeedAsync(string name, int limit) => WithUnitOfWorkAsync(async () =>
	{
		var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(
			new Building { BuildingName = "Block " + name, TotalFloors = 1 }, autoSave: true);
		var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(
			new Floor { FloorName = "Floor " + name, BuildingId = building.Id, FloorNo = 0, TotalRoom = limit }, autoSave: true);
		var type = await GetRequiredService<IRepository<RoomType, Guid>>().InsertAsync(
			new RoomType { RoomTypeName = "Type " + name, ShortName = "CR" }, autoSave: true);
		var room = new Room { RoomName = name, BuildingId = building.Id, FloorId = floor.Id,
			RoomTypeId = type.Id, Capacity = 4, BenchCapacity = 2, TenantId = GetRequiredService<ICurrentTenant>().Id };
		if (name.StartsWith("Room ", StringComparison.Ordinal))
			await GetRequiredService<IRepository<Room, Guid>>().InsertAsync(room, autoSave: true);
		return new Place(name, building.Id, floor.Id, type.Id);
	});

	private static CreateUpdateRoomDto Input(Place place) => new()
	{
		RoomName = place.Name, BuildingId = place.BuildingId, FloorId = place.FloorId, RoomTypeId = place.TypeId,
		ColumnRows = [2], BenchCapacity = 2, Capacity = 4
	};

	private sealed record Place(string Name, Guid BuildingId, Guid FloorId, Guid TypeId);
}
