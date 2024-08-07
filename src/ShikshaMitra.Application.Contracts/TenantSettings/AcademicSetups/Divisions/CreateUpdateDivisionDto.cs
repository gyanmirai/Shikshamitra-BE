using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Onboarding.Divisions;

public class CreateUpdateDivisionDto
{
    [Required]
    [StringLength(DivisionConsts.MaxNameLength)]
    public string Name { get; set; }

    public Guid StandardId { get; set; }
}