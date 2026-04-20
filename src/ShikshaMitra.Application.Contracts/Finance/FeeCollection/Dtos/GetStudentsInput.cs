using System;

namespace ShikshaMitra.Finance.FeeCollection;

public class GetStudentsInput
{
    public Guid AcademicYearId { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? DivisionId { get; set; }
    public string? SearchTerm { get; set; }
    public int SkipCount { get; set; }
    public int MaxResultCount { get; set; } = 20;
}
