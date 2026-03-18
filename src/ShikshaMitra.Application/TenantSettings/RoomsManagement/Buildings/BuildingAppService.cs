using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public class BuildingAppService(IRepository<Building, Guid> buildingRepository, IRepository<Floor, Guid> floorRepository)
: ApplicationService, IBuildingAppService
{
	public async Task<List<BuildingDto>> GetListAsync()
	{
		var buildings = await buildingRepository
			.WithDetailsAsync(b => b.Floors);

		var buildingList = await AsyncExecuter.ToListAsync(buildings);

		return ObjectMapper.Map<List<Building>, List<BuildingDto>>(buildingList);
	}

	public async Task<BuildingDto> GetAsync(Guid id)
	{
		var building = await buildingRepository
			.WithDetailsAsync(b => b.Floors);

		var entity = await AsyncExecuter.FirstOrDefaultAsync(building.Where(b => b.Id == id));

		if (entity == null)
		{
			throw new UserFriendlyException("Building not found");
		}

		return ObjectMapper.Map<Building, BuildingDto>(entity);
	}

	public async Task<BuildingDto> CreateAsync(CreateBuildingDto input)
	{
		var building = new Building
		{
			BuildingName = input.BuildingName,
			TotalFloors = input.TotalFloors,
			Description = input.Description
		};

		var createdBuilding = await buildingRepository.InsertAsync(building, autoSave: true);

		foreach (var floorDto in input.Floors)
		{
			var floor = new Floor
			{
				FloorName = floorDto.FloorName,
				TotalRoom = floorDto.TotalRoom,
				BuildingId = createdBuilding.Id,
				FloorNo = floorDto.FloorNo,
			};
			await floorRepository.InsertAsync(floor, autoSave: false);
		}

		await UnitOfWorkManager.Current.SaveChangesAsync();

		return await GetAsync(createdBuilding.Id);
	}

	public async Task<BuildingDto> UpdateAsync(Guid id, UpdateBuildingDto input)
	{
		var building = await buildingRepository.GetAsync(id);

		building.BuildingName = input.BuildingName;
		building.TotalFloors = input.TotalFloors;
		building.Description = input.Description;

		await buildingRepository.UpdateAsync(building, autoSave: true);

		var existingFloors = await AsyncExecuter.ToListAsync(
			(await floorRepository.GetQueryableAsync()).Where(f => f.BuildingId == id)
		);

		var existingFloorIds = existingFloors.Select(f => f.Id).ToList();

		// Validate that all floor IDs to update exist
		foreach (var floorDto in input.FloorsToUpdate)
		{
			if (!existingFloorIds.Contains(floorDto.Id))
			{
				throw new UserFriendlyException($"Floor with ID {floorDto.Id} not found in building {id}");
			}
		}

		// Validate that all floor IDs to delete exist
		foreach (var floorIdToDelete in input.FloorIdsToDelete)
		{
			if (!existingFloorIds.Contains(floorIdToDelete))
			{
				throw new UserFriendlyException($"Floor with ID {floorIdToDelete} not found in building {id}");
			}
		}

		// Delete floors explicitly marked for deletion
		var floorsToDelete = existingFloors
			.Where(f => input.FloorIdsToDelete.Contains(f.Id))
			.ToList();

		foreach (var floor in floorsToDelete)
		{
			await floorRepository.DeleteAsync(floor, autoSave: false);
		}

		// Update existing floors
		foreach (var floorDto in input.FloorsToUpdate)
		{
			var existingFloor = existingFloors.FirstOrDefault(f => f.Id == floorDto.Id);
			if (existingFloor != null)
			{
				existingFloor.FloorName = floorDto.FloorName;
				existingFloor.TotalRoom = floorDto.TotalRoom;
				existingFloor.FloorNo = floorDto.FloorNo;
				await floorRepository.UpdateAsync(existingFloor, autoSave: false);
			}
		}

		// Add new floors
		foreach (var floorDto in input.FloorsToAdd)
		{
			var newFloor = new Floor
			{
				FloorName = floorDto.FloorName,
				TotalRoom = floorDto.TotalRoom,
				FloorNo = floorDto.FloorNo,
				BuildingId = id
			};
			await floorRepository.InsertAsync(newFloor, autoSave: false);
		}

		await UnitOfWorkManager.Current.SaveChangesAsync();

		return await GetAsync(id);
	}

	public async Task DeleteAsync(Guid id)
	{
		var floors = await AsyncExecuter.ToListAsync(
			(await floorRepository.GetQueryableAsync()).Where(f => f.BuildingId == id)
		);

		foreach (var floor in floors)
		{
			await floorRepository.DeleteAsync(floor, autoSave: false);
		}

		await buildingRepository.DeleteAsync(id, autoSave: false);

		await UnitOfWorkManager.Current.SaveChangesAsync();
	}
}
