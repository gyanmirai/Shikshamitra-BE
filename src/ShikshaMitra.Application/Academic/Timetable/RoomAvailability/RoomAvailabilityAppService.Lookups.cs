using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public partial class RoomAvailabilityAppService
{
	public async Task<List<RoomBuildingLookupDto>> GetBuildingLookupAsync()
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View);
		var buildings = (await _buildingRepository.GetQueryableAsync())
			.Where(building => building.TenantId == CurrentTenant.Id);
		var floors = await _floorRepository.GetQueryableAsync();
		var result = await AsyncExecuter.ToListAsync(buildings.OrderBy(building => building.BuildingName)
			.ThenBy(building => building.Id).Select(building => new RoomBuildingLookupDto
			{
				Id = building.Id,
				BuildingName = building.BuildingName
			}));
		var floorRows = await AsyncExecuter.ToListAsync(
			from floor in floors
			join building in buildings on floor.BuildingId equals building.Id
			orderby floor.FloorNo, floor.FloorName, floor.Id
			select new { floor.BuildingId, Floor = new RoomFloorLookupDto
			{
				Id = floor.Id, FloorName = floor.FloorName, FloorNo = floor.FloorNo
			} });
		var floorsByBuilding = floorRows.ToLookup(row => row.BuildingId, row => row.Floor);
		foreach (var building in result)
			building.Floors = floorsByBuilding[building.Id].ToList();
		return result;
	}
}
