namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Aggregate statistics for a student's concession applications,
/// always computed across all statuses regardless of the active filter.
/// </summary>
public class ApplicationConcessionBasicDetailDto
{
    /// <summary>Total number of concession applications submitted (Pending + Approved + Rejected).</summary>
    public int TotalApplications { get; set; }

    /// <summary>Applications awaiting review.</summary>
    public int PendingApplications { get; set; }

    /// <summary>Approved applications.</summary>
    public int ApprovedApplications { get; set; }

    /// <summary>Rejected applications.</summary>
    public int RejectedApplications { get; set; }
}
