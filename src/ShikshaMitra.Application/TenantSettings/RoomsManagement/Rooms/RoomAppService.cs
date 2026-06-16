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

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class RoomAppService : CrudAppService<Room, RoomDto, Guid, GetRoomListDto, CreateUpdateRoomDto>, IRoomAppService
{
	private readonly IRepository<Building, Guid> _buildingRepository;
	private readonly IRepository<Floor, Guid> _floorRepository;
	private readonly IRepository<RoomType, Guid> _roomTypeRepository;

	public RoomAppService(
		IRepository<Room, Guid> repository,
		IRepository<Building, Guid> buildingRepository,
		IRepository<Floor, Guid> floorRepository,
		IRepository<RoomType, Guid> roomTypeRepository) : base(repository)
	{
		_buildingRepository = buildingRepository;
		_floorRepository = floorRepository;
		_roomTypeRepository = roomTypeRepository;

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

		if (input.BuildingId.HasValue)
		{
			query = query.Where(r => r.BuildingId == input.BuildingId.Value);
		}

		if (input.RoomTypeId.HasValue)
		{
			query = query.Where(r => r.RoomTypeId == input.RoomTypeId.Value);
		}

		return query;
	}

	public override async Task<RoomDto> GetAsync(Guid id)
	{
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
		// Validate that floor belongs to the specified building
		await ValidateFloorBelongsToBuildingAsync(input.FloorId, input.BuildingId);

		var room = ObjectMapper.Map<CreateUpdateRoomDto, Room>(input);
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
		// Validate that floor belongs to the specified building
		await ValidateFloorBelongsToBuildingAsync(input.FloorId, input.BuildingId);

		var room = await Repository.GetAsync(id);
		ObjectMapper.Map(input, room);
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
}
