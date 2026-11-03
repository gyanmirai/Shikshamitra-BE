using ShikshaMitra.TenantSettings.Standards;
using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Standards;

public class CreateUpdateStandardDto
{
    [Required]
    [StringLength(StandardConsts.MaxNameLength)]
    public string Name { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

	[Range(1, int.MaxValue)]
	public int AgeLowerLimit { get; set; }

	public Guid EducationMediumStageId { get; set; }

	public Guid? ShiftId { get; set; }
}