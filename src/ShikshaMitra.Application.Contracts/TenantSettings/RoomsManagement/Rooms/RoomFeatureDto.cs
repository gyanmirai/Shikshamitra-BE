using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class RoomFeatureDto : EntityDto<Guid>
{
	public string Name { get; set; } = null!;
}

public class CreateRoomFeatureDto
{
	[Required]
	[StringLength(100, MinimumLength = 1)]
	public string Name { get; set; } = null!;
}
