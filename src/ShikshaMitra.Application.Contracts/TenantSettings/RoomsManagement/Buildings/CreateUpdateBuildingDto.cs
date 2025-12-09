using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public class CreateUpdateBuildingDto
{
	[Required]
	[StringLength(BuildingConsts.MaxNameLength)]
	public string BuildingName { get; set; }

	[Required]
	[Range(1, int.MaxValue, ErrorMessage = "Total floors must be at least 1")]
	public int TotalFloors { get; set; }

	[StringLength(BuildingConsts.MaxDescriptionLength)]
	public string? Description { get; set; }

	[Required]
	public List<CreateUpdateFloorDto> Floors { get; set; } = new();
}

public class CreateUpdateFloorDto
{
	public Guid? Id { get; set; }

	[Required]
	[Range(0, int.MaxValue, ErrorMessage = "Floor number must be 0 or greater")]
	public int FloorNo { get; set; }

	[Required]
	[StringLength(FloorConsts.MaxFloorNameLength)]
	public string FloorName { get; set; }

	[Required]
	[Range(FloorConsts.MinTotalRoom, FloorConsts.MaxTotalRoom)]
	public int TotalRoom { get; set; }
}
