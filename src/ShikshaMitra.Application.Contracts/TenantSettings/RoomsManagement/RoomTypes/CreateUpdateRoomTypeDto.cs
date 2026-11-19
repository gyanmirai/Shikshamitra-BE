using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;

public class CreateUpdateRoomTypeDto
{
	[Required]
	[StringLength(RoomTypeConsts.MaxRoomTypeLength)]
	public string RoomTypeName { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

	[Required]
	[StringLength(RoomTypeConsts.MaxShortNameLength)]
	public string ShortName { get; set; } = null!;
}
