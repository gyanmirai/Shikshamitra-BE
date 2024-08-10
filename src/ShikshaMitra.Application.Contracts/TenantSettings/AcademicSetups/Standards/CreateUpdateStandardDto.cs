using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Onboarding.Standards;

public class CreateUpdateStandardDto
{
    [Required]
    [StringLength(StandardConsts.MaxNameLength)]
    public string Name { get; set; }
    
    public Guid EducationMediumStageId { get; set; }
}