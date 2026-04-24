using System;

namespace ShikshaMitra.Finance.Fees.Collections.Dtos;

public class GetComprehensiveFeeDetailsInput
{
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
}
