using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Examination.Settings;

public class GradeRange : Entity<Guid>
{
    public Guid GradingScaleId { get; private set; }
    public int MinMarks { get; private set; }
    public int MaxMarks { get; private set; }
    public string Grade { get; private set; }
    public double? GradePoint { get; private set; }
    public string? Remark { get; private set; }
    public GradeResultStatus ResultStatus { get; private set; }

    private GradeRange()
    {
        // EF materializes these required columns after this ORM constructor.
        Grade = null!;

        /* This constructor is for deserialization / ORM purpose */
    }

    public GradeRange(Guid id, Guid gradingScaleId, int minMarks, int maxMarks, string grade, double? gradePoint, string? remark, GradeResultStatus resultStatus) : base(id)
    {
        GradingScaleId = gradingScaleId;
        MinMarks = minMarks;
        MaxMarks = maxMarks;
        Grade = grade;
        GradePoint = gradePoint;
        Remark = remark;
        ResultStatus = resultStatus;
    }

    public GradeRange(Guid id, Guid gradingScaleId, int minMarks, int maxMarks, string grade) : base(id)
    {
        GradingScaleId = gradingScaleId;
        MinMarks = minMarks;
        MaxMarks = maxMarks;
        Grade = grade;
        ResultStatus = GradeResultStatus.Pass;
    }

    internal void SetGrade(string grade)
    {
        Grade = grade;
    }
}
