using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class RoomSearch_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Theory]
	[InlineData(" science ", 2)]
	[InlineData("SCIENCE", 2)]
	[InlineData("sCiEnCe", 2)]
	[InlineData(" FIRST FLOOR ", 1)]
	[InlineData("first floor", 1)]
	[InlineData("fIrSt FlOoR", 1)]
	[InlineData(" b101 ", 1)]
	[InlineData("B101", 1)]
	[InlineData("missing", 0)]
	public async Task Search_matches_building_floor_or_room_before_paging(string filter, int count)
	{
		var tenant = GetRequiredService<ICurrentTenant>();
		using var context = tenant.Change(Guid.NewGuid());
		var service = GetRequiredService<IRoomAppService>();
		var input = await WithUnitOfWorkAsync(async () =>
		{
			var building = await GetRequiredService<IRepository<Building, Guid>>()
				.InsertAsync(new Building { BuildingName = "Science Block", TotalFloors = 2 }, autoSave: true);
			var first = await GetRequiredService<IRepository<Floor, Guid>>()
				.InsertAsync(new Floor { FloorName = "First Floor", BuildingId = building.Id, FloorNo = 1, TotalRoom = 1 }, autoSave: true);
			var ground = await GetRequiredService<IRepository<Floor, Guid>>()
				.InsertAsync(new Floor { FloorName = "Ground Floor", BuildingId = building.Id, FloorNo = 0, TotalRoom = 1 }, autoSave: true);
			var type = await GetRequiredService<IRepository<RoomType, Guid>>()
				.InsertAsync(new RoomType { RoomTypeName = "Classroom", ShortName = "CR" }, autoSave: true);
			return new CreateUpdateRoomDto
			{
				RoomName = "B101", BuildingId = building.Id, FloorId = first.Id, RoomTypeId = type.Id,
				ColumnRows = [2], BenchCapacity = 2, Capacity = 4
			};
		});
		var firstRoom = await service.CreateAsync(input);
		input.RoomName = "B001";
		input.FloorId = await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<Floor, Guid>>().GetListAsync(floor =>
				floor.BuildingId == input.BuildingId && floor.FloorNo == 0)).Single().Id);
		await service.CreateAsync(input);

		var result = await service.GetListAsync(new GetRoomListDto { Filter = filter, MaxResultCount = 1, Sorting = "RoomName asc" });
		result.TotalCount.ShouldBe(count);
		result.Items.Count.ShouldBe(Math.Min(count, 1));
		if (count == 1) result.Items.Single().Id.ShouldBe(firstRoom.Id);
		if (count == 2)
		{
			var next = await service.GetListAsync(new GetRoomListDto { Filter = filter, SkipCount = 1, MaxResultCount = 1, Sorting = "RoomName asc" });
			next.Items.Single().Id.ShouldBe(firstRoom.Id);
		}
		var scoped = await service.GetListAsync(new GetRoomListDto { Filter = filter, FloorId = input.FloorId });
		scoped.TotalCount.ShouldBe(count == 2 ? 1 : 0);
		using (tenant.Change(Guid.NewGuid()))
			(await service.GetListAsync(new GetRoomListDto { Filter = filter })).TotalCount.ShouldBe(0);
	}
}
