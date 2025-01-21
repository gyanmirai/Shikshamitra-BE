using ShikshaMitra.TenantSettings.Standards;
using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Standards;

public class CreateUpdateStandardDto
{
    [Required]
    [StringLength(StandardConsts.MaxNameLength)]
    public string Name { get; set; }
    
    public Guid EducationMediumStageId { get; set; }
}