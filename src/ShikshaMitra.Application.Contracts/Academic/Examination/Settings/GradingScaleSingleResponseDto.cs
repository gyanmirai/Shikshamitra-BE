namespace ShikshaMitra.Academic.Examination.Settings;

public class GradingScaleSingleResponseDto
{
    public GradingScaleDto Data { get; set; }

    public GradingScaleSingleResponseDto(GradingScaleDto data)
    {
        Data = data;
    }
}
