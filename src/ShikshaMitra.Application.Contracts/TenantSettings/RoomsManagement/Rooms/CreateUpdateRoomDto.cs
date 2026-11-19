using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class CreateUpdateRoomDto
{
	[Required]
	[StringLength(RoomConsts.MaxRoomNameLength)]
	public string RoomName { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

	[Required]
	[Range(0, int.MaxValue)]
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
	public int[]? ColumnRows { get; set; }

	public List<string>? Features { get; set; }
	public RoomImageInput? Image { get; set; }
	public bool RemoveImage { get; set; }

	public string? BoardPosition { get; set; }
	public string? EntrancePosition { get; set; }
	public string? DisabledSeatNumbersCsv { get; set; }
	public string? ReservedSeatNumbersCsv { get; set; }
	public string? AccessibleSeatNumbersCsv { get; set; }
	public string? AisleAfterColumnsCsv { get; set; }
}
