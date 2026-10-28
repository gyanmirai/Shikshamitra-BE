using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class GetRoomListDto : PagedAndSortedResultRequestDto
{
	public string? Filter { get; set; }
	public Guid? BuildingId { get; set; }
	public Guid? FloorId { get; set; }
	public Guid? RoomTypeId { get; set; }
	[System.ComponentModel.DataAnnotations.StringLength(16384)]
	public string? ColumnFilters { get; set; }
}
