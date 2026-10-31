using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Examination.Settings;

public class UpdateGradingScaleDto
{
    [Required]
    public string Name { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

    [Required]
    public string TotalMarks { get; set; } = null!;

    public double? PassMarks { get; set; }

    public string? Description { get; set; }

    public bool IsDefault { get; set; }

    public SettingStatus Status { get; set; }

    [Required]
    [MinLength(1)]
    public List<GradeRangeDto> GradingScale { get; set; } = null!;
}
