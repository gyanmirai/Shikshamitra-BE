using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;

public class RoomType : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public string RoomTypeName { get; set; } = null!; // Required persisted column; populated by EF or the owning create flow.
	public string ShortName { get; set; } = null!; // Required persisted column; populated by EF or the owning create flow.
}
