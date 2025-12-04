using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class Room : AuditedAggregateRoot<Guid>
{
	public string RoomName { get; set; }
	public int Capacity { get; set; }
	public Guid BuildingId { get; set; }
	public int Floor { get; set; }
	public Guid RoomTypeId { get; set; }
	
	// Benches Details
	public int TotalBenches { get; set; }
	public int BenchCapacity { get; set; }
	public int ClassBenchesRows { get; set; }
	public int ClassBenchesColumns { get; set; }
	
	// Features (stored as comma-separated string)
	public string Features { get; set; }
}
