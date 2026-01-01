using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Examination.Settings;

public class CreateGradingScaleDto
{
    [Required]
    public string Name { get; set; }

    [Required]
    public string TotalMarks { get; set; }

    [Required]
    [MinLength(1)]
    public List<GradeRangeDto> GradingScale { get; set; }
}
