using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;

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
		var building = await _buildingRepository.GetAsync(room.BuildingId);
		var floor = await _floorRepository.GetAsync(room.FloorId);
		var roomType = await _roomTypeRepository.GetAsync(room.RoomTypeId);

		var dto = ObjectMapper.Map<Room, RoomDto>(room);
		dto.BuildingName = building.BuildingName;
		dto.FloorName = floor.FloorName;
		dto.RoomTypeName = roomType.RoomTypeName;

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
}
