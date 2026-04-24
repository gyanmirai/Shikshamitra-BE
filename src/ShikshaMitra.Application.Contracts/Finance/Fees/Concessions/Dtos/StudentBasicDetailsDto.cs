using System;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Lightweight student profile included in the student concession-requests list response.
/// </summary>
public class StudentBasicDetailsDto
{
    public Guid StudentId { get; set; }

    /// <summary>Student full name (first + middle + last).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Roll number as a display string.</summary>
    public string RollNo { get; set; } = string.Empty;

    /// <summary>GR (admission / registration) number.</summary>
    public string GrNumber { get; set; } = string.Empty;

    /// <summary>Class / standard designation (e.g. "Class X-B").</summary>
    public string StandardName { get; set; } = string.Empty;

    /// <summary>Section / division name (e.g. "Section B").</summary>
    public string DivisionName { get; set; } = string.Empty;

    /// <summary>Education board name (e.g. "CBSE").</summary>
    public string BoardName { get; set; } = string.Empty;

    /// <summary>Education medium name (e.g. "English").</summary>
    public string EducationMediumName { get; set; } = string.Empty;
}
