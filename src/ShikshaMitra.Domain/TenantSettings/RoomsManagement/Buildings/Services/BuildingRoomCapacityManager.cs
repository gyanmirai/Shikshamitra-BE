using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public class BuildingRoomCapacityManager(
	IRepository<Building, Guid> buildings,
	IRepository<Room, Guid> rooms,
	IBuildingRoomCapacityLock capacityLock) : DomainService
{
	public async Task LockBuildingAsync(Guid buildingId)
	{
		await capacityLock.AcquireAsync(buildingId);
		await buildings.GetAsync(buildingId);
	}

	public async Task EnsureRoomSpaceAsync(Floor floor, Guid? currentRoomId = null)
	{
		var query = (await rooms.GetQueryableAsync()).Where(room => room.FloorId == floor.Id);
		if (currentRoomId.HasValue) query = query.Where(room => room.Id != currentRoomId.Value);
		var count = await AsyncExecuter.CountAsync(query);
		if (count >= floor.TotalRoom)
			throw LimitError(RoomsManagementErrorCodes.RoomFloorLimitReached, floor, floor.TotalRoom, count);
	}

	public async Task EnsureFloorLimitAsync(Floor floor, int limit)
	{
		var count = await rooms.CountAsync(room => room.FloorId == floor.Id);
		if (limit < count)
			throw LimitError(RoomsManagementErrorCodes.FloorRoomLimitBelowUsage, floor, limit, count);
	}

	public async Task<Dictionary<Guid, int>> GetRoomCountsAsync(Guid[] buildingIds)
	{
		if (buildingIds.Length == 0) return [];
		var query = (await rooms.GetQueryableAsync()).Where(room => Enumerable.Contains(buildingIds, room.BuildingId))
			.GroupBy(room => room.FloorId).Select(group => new { FloorId = group.Key, Count = group.Count() });
		return (await AsyncExecuter.ToListAsync(query)).ToDictionary(group => group.FloorId, group => group.Count);
	}

	private static BusinessException LimitError(string code, Floor floor, int limit, int count) => new BusinessException(code)
		.WithData("BuildingId", floor.BuildingId).WithData("FloorId", floor.Id).WithData("FloorName", floor.FloorName)
		.WithData("RoomLimit", limit).WithData("RoomCount", count);
}
