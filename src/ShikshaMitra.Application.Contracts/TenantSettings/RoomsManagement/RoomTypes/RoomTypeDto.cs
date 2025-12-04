using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;

public class RoomTypeDto : AuditedEntityDto<Guid>
{
	public string RoomTypeName { get; set; }
	public string ShortName { get; set; }
}
