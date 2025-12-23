using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class CreateUpdateRoomDto
{
	[Required]
	[StringLength(RoomConsts.MaxRoomNameLength)]
	public string RoomName { get; set; }

	[Required]
	[Range(1, int.MaxValue)]
	public int Capacity { get; set; }

	[Required]
	public Guid BuildingId { get; set; }

	[Required]
	public Guid FloorId { get; set; }

	[Required]
	public Guid RoomTypeId { get; set; }

	[Required]
	[Range(0, int.MaxValue)]
	public int TotalBenches { get; set; }

	[Required]
	[Range(0, int.MaxValue)]
	public int BenchCapacity { get; set; }

	[Required]
	[Range(0, int.MaxValue)]
	public int BenchesPerRow { get; set; }

	[Required]
	[Range(0, int.MaxValue)]
	public int BenchesPerColumn { get; set; }

	public List<string>? Features { get; set; }
}
