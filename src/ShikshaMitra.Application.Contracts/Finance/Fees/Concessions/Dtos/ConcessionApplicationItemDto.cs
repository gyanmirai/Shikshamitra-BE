using System;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// A single row in the concession-applications list view.
/// Each row represents one <c>ConcessionRequest</c> record (one concession type per student)
/// enriched with the student's basic profile.
/// </summary>
public class ConcessionApplicationItemDto
{
    /// <summary>Unique identifier of the concession request.</summary>
    public Guid ConcessionRequestId { get; set; }

    public Guid StudentId { get; set; }

    /// <summary>Student full name (first + middle + last).</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>Roll number as a display string.</summary>
    public string RollNo { get; set; } = string.Empty;

    /// <summary>GR (admission / registration) number. Null when not yet assigned.</summary>
    public string? GrNumber { get; set; }

    /// <summary>Class / standard designation (e.g. "10-A").</summary>
    public string StandardName { get; set; } = string.Empty;

    /// <summary>Section / division name (e.g. "A").</summary>
    public string DivisionName { get; set; } = string.Empty;

    /// <summary>Student primary email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Student primary contact number.</summary>
    public string ContactNo { get; set; } = string.Empty;

    /// <summary>
    /// API-level status: 0 = Pending, 1 = Approved, 2 = Rejected, 3 = Draft.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Total number of concession-type requests submitted by this student across all statuses.
    /// </summary>
    public int TotalConcessionApplication { get; set; }
}
