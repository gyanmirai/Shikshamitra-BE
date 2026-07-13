using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Examination.Settings;

public class CreateGradingScaleDto
{
    [Required]
    public string Name { get; set; }

    [Required]
    public string TotalMarks { get; set; }

    public double? PassMarks { get; set; }

    public string? Description { get; set; }

    public bool IsDefault { get; set; }

    [Required]
    [MinLength(1)]
    public List<GradeRangeDto> GradingScale { get; set; }
}
