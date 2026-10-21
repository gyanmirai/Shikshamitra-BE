using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using ShikshaMitra.Constants.Permissions;
using System.Collections.Generic;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public partial class RoomAppService : CrudAppService<Room, RoomDto, Guid, GetRoomListDto, CreateUpdateRoomDto>, IRoomAppService
{
	private readonly IRepository<Building, Guid> _buildingRepository;
	private readonly IRepository<Floor, Guid> _floorRepository;
	private readonly IRepository<RoomType, Guid> _roomTypeRepository;
	private readonly IRepository<RoomFeature, Guid> _featureRepository;

	public RoomAppService(
		IRepository<Room, Guid> repository,
		IRepository<Building, Guid> buildingRepository,
		IRepository<Floor, Guid> floorRepository,
		IRepository<RoomType, Guid> roomTypeRepository,
		IRepository<RoomFeature, Guid> featureRepository) : base(repository)
	{
		_buildingRepository = buildingRepository;
		_floorRepository = floorRepository;
		_roomTypeRepository = roomTypeRepository;
		_featureRepository = featureRepository;

		// Set permission policies for CRUD operations
		GetPolicyName = ShikshaMitraSubmodulePermissions.InstituteSetup.RoomManagement.View;
		GetListPolicyName = ShikshaMitraSubmodulePermissions.InstituteSetup.RoomManagement.View;
		CreatePolicyName = ShikshaMitraSubmodulePermissions.InstituteSetup.RoomManagement.Add;
		UpdatePolicyName = ShikshaMitraSubmodulePermissions.InstituteSetup.RoomManagement.Add;
		DeletePolicyName = ShikshaMitraSubmodulePermissions.InstituteSetup.RoomManagement.Delete;
	}

	private async Task ValidateFloorBelongsToBuildingAsync(Guid floorId, Guid buildingId)
	{
		if (floorId == Guid.Empty)
		{
			throw new UserFriendlyException("FloorId is required and cannot be empty.");
		}

		var floor = await _floorRepository.FindAsync(floorId);
		if (floor == null)
		{
			throw new UserFriendlyException($"Floor with ID {floorId} not found.");
		}

		if (floor.BuildingId != buildingId)
		{
			throw new UserFriendlyException($"Floor with ID {floorId} does not belong to the specified building.");
		}
	}

	protected override async Task<IQueryable<Room>> CreateFilteredQueryAsync(GetRoomListDto input)
	{
		var query = await base.CreateFilteredQueryAsync(input);

		if (!string.IsNullOrWhiteSpace(input.Filter))
		{
			var filter = input.Filter.Trim().ToLowerInvariant();
			var buildings = await _buildingRepository.GetQueryableAsync();
			var floors = await _floorRepository.GetQueryableAsync();
			var matchingBuildingIds = buildings
				.Where(building => building.BuildingName.ToLower().Contains(filter))
				.Select(building => building.Id);
			var matchingFloorIds = floors
				.Where(floor => floor.FloorName.ToLower().Contains(filter))
				.Select(floor => floor.Id);
			query = query.Where(room => room.RoomName.ToLower().Contains(filter)
				|| matchingBuildingIds.Contains(room.BuildingId)
				|| matchingFloorIds.Contains(room.FloorId));
		}

		if (input.BuildingId.HasValue)
		{
			query = query.Where(r => r.BuildingId == input.BuildingId.Value);
		}

		if (input.FloorId.HasValue)
		{
			query = query.Where(r => r.FloorId == input.FloorId.Value);
		}

		if (input.RoomTypeId.HasValue)
		{
			query = query.Where(r => r.RoomTypeId == input.RoomTypeId.Value);
		}

		return query;
	}

	public override async Task<RoomDto> GetAsync(Guid id)
	{
		await CheckGetPolicyAsync();
		var room = await Repository.GetAsync(id);
		var building = await _buildingRepository.FindAsync(room.BuildingId);
		var floor = await _floorRepository.FindAsync(room.FloorId);
		var roomType = await _roomTypeRepository.FindAsync(room.RoomTypeId);

		var dto = ObjectMapper.Map<Room, RoomDto>(room);
		dto.BuildingName = building?.BuildingName;
		dto.FloorName = floor?.FloorName;
		dto.RoomTypeName = roomType?.RoomTypeName;

		return dto;
	}

	public override async Task<PagedResultDto<RoomDto>> GetListAsync(GetRoomListDto input)
	{
		await CheckGetListPolicyAsync();
		var query = await CreateFilteredQueryAsync(input);
		var totalCount = await AsyncExecuter.CountAsync(query);

		query = ApplySorting(query, input);
		query = ApplyPaging(query, input);

		var rooms = await AsyncExecuter.ToListAsync(query);
		var roomDtos = ObjectMapper.Map<System.Collections.Generic.List<Room>, System.Collections.Generic.List<RoomDto>>(rooms);

		// Load building and room type names
		foreach (var dto in roomDtos)
		{
			var building = await _buildingRepository.FindAsync(dto.BuildingId);
			var floor = await _floorRepository.FindAsync(dto.FloorId);
			var roomType = await _roomTypeRepository.FindAsync(dto.RoomTypeId);

			dto.BuildingName = building?.BuildingName;
			dto.FloorName = floor?.FloorName;
			dto.RoomTypeName = roomType?.RoomTypeName;
		}

		return new PagedResultDto<RoomDto>(totalCount, roomDtos);
	}

	public override async Task<RoomDto> CreateAsync(CreateUpdateRoomDto input)
	{
		await CheckCreatePolicyAsync();
		// Validate that floor belongs to the specified building
		await ValidateFloorBelongsToBuildingAsync(input.FloorId, input.BuildingId);

		var room = ObjectMapper.Map<CreateUpdateRoomDto, Room>(input);
		room.TenantId = CurrentTenant.Id;
		room.SetColumnLayout(input.ColumnRows, input.BenchCapacity);
		room.Features = await ResolveFeaturesAsync(input.Features);
		await ApplyImageAsync(room, input);
		await Repository.InsertAsync(room, autoSave: true);

		var building = await _buildingRepository.GetAsync(room.BuildingId);
		var floor = await _floorRepository.GetAsync(room.FloorId);
		var roomType = await _roomTypeRepository.GetAsync(room.RoomTypeId);

		var dto = ObjectMapper.Map<Room, RoomDto>(room);
		dto.BuildingName = building.BuildingName;
		dto.FloorName = floor.FloorName;
		dto.RoomTypeName = roomType.RoomTypeName;

		return dto;
	}

	public override async Task<RoomDto> UpdateAsync(Guid id, CreateUpdateRoomDto input)
	{
		await CheckUpdatePolicyAsync();
		// Validate that floor belongs to the specified building
		await ValidateFloorBelongsToBuildingAsync(input.FloorId, input.BuildingId);

		var room = await Repository.GetAsync(id);
		ObjectMapper.Map(input, room);
		room.SetColumnLayout(input.ColumnRows, input.BenchCapacity);
		room.Features = await ResolveFeaturesAsync(input.Features);
		await ApplyImageAsync(room, input);
		await Repository.UpdateAsync(room, autoSave: true);

		var building = await _buildingRepository.GetAsync(room.BuildingId);
		var floor = await _floorRepository.GetAsync(room.FloorId);
		var roomType = await _roomTypeRepository.GetAsync(room.RoomTypeId);

		var dto = ObjectMapper.Map<Room, RoomDto>(room);
		dto.BuildingName = building.BuildingName;
		dto.FloorName = floor.FloorName;
		dto.RoomTypeName = roomType.RoomTypeName;

		return dto;
	}

	public async Task<ListResultDto<RoomFeatureDto>> GetFeaturesAsync()
	{
		await CheckGetListPolicyAsync();
		var features = await _featureRepository.GetListAsync();
		return new ListResultDto<RoomFeatureDto>(features.OrderBy(x => x.CreationTime).ThenBy(x => x.Name)
			.Select(x => new RoomFeatureDto { Id = x.Id, Name = x.Name }).ToList());
	}

	public async Task<RoomFeatureDto> CreateFeatureAsync(CreateRoomFeatureDto input)
	{
		await CheckCreatePolicyAsync();
		var feature = new RoomFeature(GuidGenerator.Create(), CurrentTenant.Id, input.Name);
		var existing = await _featureRepository.FindAsync(x => x.NormalizedName == feature.NormalizedName);
		if (existing != null) return new RoomFeatureDto { Id = existing.Id, Name = existing.Name };
		await _featureRepository.InsertAsync(feature, autoSave: true);
		return new RoomFeatureDto { Id = feature.Id, Name = feature.Name };
	}

	private async Task<List<string>> ResolveFeaturesAsync(List<string>? names)
	{
		if (names == null || names.Count == 0) return [];
		var catalogue = await _featureRepository.GetListAsync();
		var selected = new List<string>();
		foreach (var name in names)
		{
			var feature = catalogue.Find(x => string.Equals(x.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));
			if (feature == null) throw new BusinessException("Rooms:UnknownFeature").WithData("Name", name);
			if (!selected.Contains(feature.Name)) selected.Add(feature.Name);
		}
		return selected;
	}
}
