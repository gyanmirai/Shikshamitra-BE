using System.ComponentModel.DataAnnotations;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

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
	public string Description { get; set; }
}
