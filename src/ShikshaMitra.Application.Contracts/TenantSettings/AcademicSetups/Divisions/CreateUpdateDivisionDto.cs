using ShikshaMitra.TenantSettings.Divisions;
using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Divisions;

public class CreateUpdateDivisionDto
{
	[Required]
	[StringLength(DivisionConsts.MaxNameLength)]
	public string Name { get; set; }

	[Required]
	[Range(1, DivisionConsts.MaxStrengthCount)]
	public int Strength { get; set; }

	public string RoomNumber { get; set; }

	public Guid StandardId { get; set; }
}