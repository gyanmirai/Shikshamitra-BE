using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class Room : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
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
	
	// Features (stored as comma-separated string, optional)
	public string? Features { get; set; }
}
