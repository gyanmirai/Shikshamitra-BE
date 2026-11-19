using System;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Category { get; set; }
    public string Color { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public SubjectMasterStatus Status { get; set; }
    public int? SortOrder { get; set; }
    public Guid EducationMediumId { get; set; }
}
