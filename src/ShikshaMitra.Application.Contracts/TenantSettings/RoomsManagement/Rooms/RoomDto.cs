using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class RoomDto : AuditedEntityDto<Guid>
{
	public string RoomName { get; set; }
	public bool HasImage { get; set; }
	public int Capacity { get; set; }
	public Guid BuildingId { get; set; }
	public string BuildingName { get; set; }
	public Guid FloorId { get; set; }
	public string FloorName { get; set; }
	public Guid RoomTypeId { get; set; }
	public string RoomTypeName { get; set; }
	public int TotalBenches { get; set; }
	public int BenchCapacity { get; set; }
	public int BenchesPerRow { get; set; }
	public int BenchesPerColumn { get; set; }
	public int[]? ColumnRows { get; set; }
	public List<string>? Features { get; set; }
	public string? BoardPosition { get; set; }
	public string? EntrancePosition { get; set; }
	public string? DisabledSeatNumbersCsv { get; set; }
	public string? ReservedSeatNumbersCsv { get; set; }
	public string? AccessibleSeatNumbersCsv { get; set; }
	public string? AisleAfterColumnsCsv { get; set; }
}
