using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class GetRoomListDto : PagedAndSortedResultRequestDto
{
	public Guid? BuildingId { get; set; }
	public Guid? FloorId { get; set; }
	public Guid? RoomTypeId { get; set; }
}
