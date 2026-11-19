using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;

public class CreateUpdateEducationMediumDto
{
    [Required]
    [StringLength(EducationMediumConsts.MaxNameLength)]
    public string Name { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

    public Guid EducationBoardId { get; set; }
}