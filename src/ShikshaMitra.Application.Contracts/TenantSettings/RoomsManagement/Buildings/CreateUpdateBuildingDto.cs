using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public abstract class BuildingMutationDtoBase
{
	[Required]
	[StringLength(BuildingConsts.MaxNameLength)]
	public string BuildingName { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

	[Required]
	[Range(1, int.MaxValue, ErrorMessage = "Total floors must be at least 1")]
	public int TotalFloors { get; set; }

	[StringLength(BuildingConsts.MaxDescriptionLength)]
	public string? Description { get; set; }
}

public class CreateBuildingDto : BuildingMutationDtoBase
{
	[Required]
	public List<CreateFloorDto> Floors { get; set; } = new();
}

public class UpdateBuildingDto : BuildingMutationDtoBase
{
	public List<UpdateFloorDto> FloorsToUpdate { get; set; } = new();

	public List<CreateFloorDto> FloorsToAdd { get; set; } = new();

	public List<Guid> FloorIdsToDelete { get; set; } = new();
}

public abstract class FloorMutationDtoBase
{
	[Required]
	[Range(0, int.MaxValue, ErrorMessage = "Floor number must be 0 or greater")]
	public int FloorNo { get; set; }

	[Required]
	[StringLength(FloorConsts.MaxFloorNameLength)]
	public string FloorName { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

	[Required]
	[Range(FloorConsts.MinTotalRoom, FloorConsts.MaxTotalRoom)]
	public int TotalRoom { get; set; }
}

public class CreateFloorDto : FloorMutationDtoBase
{
}

public class UpdateFloorDto : FloorMutationDtoBase
{
	[Required]
	public Guid Id { get; set; }
}
