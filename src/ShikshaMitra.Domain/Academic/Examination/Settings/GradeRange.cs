using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Examination.Settings;

public class GradeRange : Entity<Guid>
{
    public Guid GradingScaleId { get; private set; }
    public int MinMarks { get; private set; }
    public int MaxMarks { get; private set; }
    public string Grade { get; private set; }

    private GradeRange()
    {
        /* This constructor is for deserialization / ORM purpose */
    }

    public GradeRange(Guid id, Guid gradingScaleId, int minMarks, int maxMarks, string grade) : base(id)
    {
        GradingScaleId = gradingScaleId;
        MinMarks = minMarks;
        MaxMarks = maxMarks;
        Grade = grade;
    }

    internal void SetGrade(string grade)
    {
        Grade = grade;
    }
}
