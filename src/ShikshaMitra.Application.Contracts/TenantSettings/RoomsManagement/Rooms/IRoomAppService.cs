using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public interface IRoomAppService : ICrudAppService<RoomDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateRoomDto>
{
}
