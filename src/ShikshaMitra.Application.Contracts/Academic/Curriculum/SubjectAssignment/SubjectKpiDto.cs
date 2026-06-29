namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// KPI summary cards data for the subject allocation list page.
/// </summary>
public class SubjectKpiDto
{
    public int TotalSubjects { get; set; }
    public int CoreSubjects { get; set; }
    public int ElectiveSubjects { get; set; }
    public int OptionalSubjects { get; set; }
    public int SubjectsWithTeacher { get; set; }
    public int SubjectsMissingTeacher { get; set; }
    public int PublishedSubjects { get; set; }
    public int DraftSubjects { get; set; }

    /// <summary>Average setup health across all subjects (0-100)</summary>
    public int OverallHealthScore { get; set; }
}
