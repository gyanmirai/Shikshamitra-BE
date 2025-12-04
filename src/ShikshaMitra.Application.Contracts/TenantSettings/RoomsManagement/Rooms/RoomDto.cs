using System;
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
	public int ClassBenchesRows { get; set; }
	public int ClassBenchesColumns { get; set; }
	public string Features { get; set; }
}
