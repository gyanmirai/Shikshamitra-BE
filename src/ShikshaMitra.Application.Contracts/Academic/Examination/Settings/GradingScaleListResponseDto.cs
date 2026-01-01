using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.Settings;

public class GradingScaleListResponseDto
{
    public List<GradingScaleDto> Data { get; set; }

    public GradingScaleListResponseDto()
    {
        Data = new List<GradingScaleDto>();
    }

    public GradingScaleListResponseDto(List<GradingScaleDto> data)
    {
        Data = data ?? new List<GradingScaleDto>();
    }
}
