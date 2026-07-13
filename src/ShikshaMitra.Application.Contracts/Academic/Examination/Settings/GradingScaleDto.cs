using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Examination.Settings;

public class GradingScaleDto : EntityDto<Guid>
{
    public string Name { get; set; }
    public string TotalMarks { get; set; }
    public double? PassMarks { get; set; }
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public SettingStatus Status { get; set; }
    public int UsageCount { get; set; }
    public List<GradeRangeDto> GradingScale { get; set; }
    public DateTime CreatedAt { get; set; }
}
