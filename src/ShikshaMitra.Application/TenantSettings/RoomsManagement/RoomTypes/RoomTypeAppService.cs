using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;

public class RoomTypeAppService(IRepository<RoomType, Guid> repository)
	: CrudAppService<RoomType, RoomTypeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateRoomTypeDto>(repository),
	IRoomTypeAppService
{
}
