using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class RoomDto : AuditedEntityDto<Guid>
{
	public string RoomName { get; set; }
	public int Capacity { get; set; }
	public Guid BuildingId { get; set; }
	public string BuildingName { get; set; }
	public int Floor { get; set; }
	public Guid RoomTypeId { get; set; }
	public string RoomTypeName { get; set; }
	public int TotalBenches { get; set; }
	public int BenchCapacity { get; set; }
	public int BenchesPerRow { get; set; }
	public int BenchesPerColumn { get; set; }
	public List<string>? Features { get; set; }
}
