using System;
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
	[Range(1, int.MaxValue)]
	public int Floor { get; set; }

	[Required]
	public Guid RoomTypeId { get; set; }

	[Required]
	[Range(0, int.MaxValue)]
	public int TotalBenches { get; set; }

	[Range(0, int.MaxValue)]
	public int BenchCapacity { get; set; }

	[Range(0, int.MaxValue)]
	public int ClassBenchesRows { get; set; }

	[Range(0, int.MaxValue)]
	public int ClassBenchesColumns { get; set; }

	[StringLength(RoomConsts.MaxFeaturesLength)]
	public string? Features { get; set; }
}
