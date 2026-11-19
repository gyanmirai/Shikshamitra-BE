using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;

public class RoomTypeDto : AuditedEntityDto<Guid>
{
	public string RoomTypeName { get; set; } = null!; // The response mapper populates these members after construction.
	public string ShortName { get; set; } = null!;
}
