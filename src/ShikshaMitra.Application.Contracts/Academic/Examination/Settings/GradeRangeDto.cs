namespace ShikshaMitra.Academic.Examination.Settings;

public class GradeRangeDto
{
    public int MinMarks { get; set; }
    public int MaxMarks { get; set; }
    public string Grade { get; set; }
    public double? GradePoint { get; set; }
    public string? Remark { get; set; }
    public GradeResultStatus ResultStatus { get; set; }
}
