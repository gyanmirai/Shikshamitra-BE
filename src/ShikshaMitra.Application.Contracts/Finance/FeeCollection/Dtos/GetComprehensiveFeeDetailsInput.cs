using System;

namespace ShikshaMitra.Finance.FeeCollection;

public class GetComprehensiveFeeDetailsInput
{
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
}
