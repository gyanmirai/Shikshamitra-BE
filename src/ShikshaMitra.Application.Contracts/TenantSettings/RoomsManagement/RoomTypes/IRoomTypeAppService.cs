using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;

public interface IRoomTypeAppService : ICrudAppService<RoomTypeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateRoomTypeDto>
{
}
