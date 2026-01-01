using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Examination.Settings;

public class GradingScaleDto : EntityDto<Guid>
{
    public string Name { get; set; }
    public string TotalMarks { get; set; }
    public List<GradeRangeDto> GradingScale { get; set; }
    public DateTime CreatedAt { get; set; }
}
