using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;

public class RoomType : AuditedAggregateRoot<Guid>
{
	public string RoomTypeName { get; set; }
	public string ShortName { get; set; }
}
