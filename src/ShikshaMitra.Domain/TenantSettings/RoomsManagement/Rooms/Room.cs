using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class Room : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public string RoomName { get; set; }
	public int Capacity { get; set; }
	public Guid BuildingId { get; set; }
	public Guid FloorId { get; set; }
	public Guid RoomTypeId { get; set; }
	
	// Benches Details
	public int TotalBenches { get; set; }
	public int BenchCapacity { get; set; }
	public int BenchesPerRow { get; set; }
	public int BenchesPerColumn { get; set; }
	
	// Features (stored as JSON array)
	public List<string>? Features { get; set; }
}
