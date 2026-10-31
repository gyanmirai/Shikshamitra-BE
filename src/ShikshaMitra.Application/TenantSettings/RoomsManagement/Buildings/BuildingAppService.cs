using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.Formatting;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public class BuildingAppService(IRepository<Building, Guid> buildingRepository, IRepository<Floor, Guid> floorRepository,
	BuildingRoomCapacityManager capacity)
: ApplicationService, IBuildingAppService
{
	private InstituteFieldFormattingStore Formatting => LazyServiceProvider.LazyGetRequiredService<InstituteFieldFormattingStore>();
	public async Task<List<BuildingDto>> GetListAsync()
	{
		var buildings = await buildingRepository
			.WithDetailsAsync(b => b.Floors);

		var buildingList = await AsyncExecuter.ToListAsync(buildings);

		var result = ObjectMapper.Map<List<Building>, List<BuildingDto>>(buildingList);
		await PopulateRoomCountsAsync(result);
		return result;
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

		var result = ObjectMapper.Map<Building, BuildingDto>(entity);
		await PopulateRoomCountsAsync([result]);
		return result;
	}

	[UnitOfWork(isTransactional: true)]
	public async Task<BuildingDto> CreateAsync(CreateBuildingDto input)
	{
		var rules = await Formatting.GetAsync("buildings");
		var building = new Building
		{
			BuildingName = InstituteFieldFormattingCatalog.Required(rules, "buildingName", input.BuildingName, BuildingConsts.MaxNameLength),
			TotalFloors = input.TotalFloors,
			Description = InstituteFieldFormattingCatalog.Optional(rules, "description", input.Description, BuildingConsts.MaxDescriptionLength)
		};

		var createdBuilding = await buildingRepository.InsertAsync(building, autoSave: false);

		foreach (var floorDto in input.Floors)
		{
			var floor = new Floor
			{
				FloorName = InstituteFieldFormattingCatalog.Required(rules, "floorName", floorDto.FloorName, FloorConsts.MaxFloorNameLength),
				TotalRoom = floorDto.TotalRoom,
				BuildingId = createdBuilding.Id,
				FloorNo = floorDto.FloorNo,
			};
			await floorRepository.InsertAsync(floor, autoSave: false);
		}

		await UnitOfWorkManager.Current!.SaveChangesAsync();

		return await GetAsync(createdBuilding.Id);
	}

	[UnitOfWork(isTransactional: true)]
	public async Task<BuildingDto> UpdateAsync(Guid id, UpdateBuildingDto input)
	{
		var building = await capacity.LockBuildingAsync(id);

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

		var counts = await capacity.GetRoomCountsAsync([id]);
		var floorsById = existingFloors.ToDictionary(floor => floor.Id);
		foreach (var floorDto in input.FloorsToUpdate)
			capacity.EnsureFloorLimit(floorsById[floorDto.Id], floorDto.TotalRoom, counts.GetValueOrDefault(floorDto.Id));
		building.BuildingName = input.BuildingName;
		building.TotalFloors = input.TotalFloors;
		building.Description = input.Description;
		await buildingRepository.UpdateAsync(building, autoSave: false);

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
			var existingFloor = floorsById.GetValueOrDefault(floorDto.Id);
			if (existingFloor != null)
			{
				existingFloor.FloorName = floorDto.FloorName;
				existingFloor.TotalRoom = floorDto.TotalRoom;
				existingFloor.FloorNo = floorDto.FloorNo;
				await floorRepository.UpdateAsync(existingFloor, autoSave: false);
			}
		}

		// Add new floors
		var rules = input.FloorsToAdd.Count > 0 ? await Formatting.GetAsync("buildings") : null;
		foreach (var floorDto in input.FloorsToAdd)
		{
			var newFloor = new Floor
			{
				FloorName = InstituteFieldFormattingCatalog.Required(rules!, "floorName", floorDto.FloorName, FloorConsts.MaxFloorNameLength),
				TotalRoom = floorDto.TotalRoom,
				FloorNo = floorDto.FloorNo,
				BuildingId = id
			};
			await floorRepository.InsertAsync(newFloor, autoSave: false);
		}

		await UnitOfWorkManager.Current!.SaveChangesAsync();

		return await GetAsync(id);
	}

	private async Task PopulateRoomCountsAsync(List<BuildingDto> buildings)
	{
		var counts = await capacity.GetRoomCountsAsync(buildings.Select(building => building.Id).ToArray());
		foreach (var floor in buildings.SelectMany(building => building.Floors))
			floor.RoomCount = counts.GetValueOrDefault(floor.Id);
	}

	[UnitOfWork(isTransactional: true)]
	public async Task DeleteAsync(Guid id)
	{
		await capacity.LockBuildingAsync(id);
		var floors = await AsyncExecuter.ToListAsync(
			(await floorRepository.GetQueryableAsync()).Where(f => f.BuildingId == id)
		);

		foreach (var floor in floors)
		{
			await floorRepository.DeleteAsync(floor, autoSave: false);
		}

		await buildingRepository.DeleteAsync(id, autoSave: false);

		await UnitOfWorkManager.Current!.SaveChangesAsync();
	}
}
