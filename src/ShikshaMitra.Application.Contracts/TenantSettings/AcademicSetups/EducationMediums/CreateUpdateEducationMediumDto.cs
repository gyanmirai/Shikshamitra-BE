using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Onboarding.EducationMediums;

public class CreateUpdateEducationMediumDto
{
    [Required]
    [StringLength(EducationMediumConsts.MaxNameLength)]
    public string Name { get; set; }

    public Guid EducationBoardId { get; set; }
}