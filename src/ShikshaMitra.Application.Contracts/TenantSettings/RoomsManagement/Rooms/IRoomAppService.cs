using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public interface IRoomAppService : ICrudAppService<RoomDto, Guid, GetRoomListDto, CreateUpdateRoomDto>
{
	System.Threading.Tasks.Task<RoomImageDto?> GetImageAsync(Guid id);
	System.Threading.Tasks.Task<ListResultDto<RoomFeatureDto>> GetFeaturesAsync();
	System.Threading.Tasks.Task<RoomFeatureDto> CreateFeatureAsync(CreateRoomFeatureDto input);
}
