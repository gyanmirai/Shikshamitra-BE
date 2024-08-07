using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Onboarding.EducationalMediums;

public class CreateUpdateEducationalMediumDto
{
    [Required]
    [StringLength(EducationalMediumConsts.MaxNameLength)]
    public string Name { get; set; }

    public Guid EducationBoardId { get; set; }
}