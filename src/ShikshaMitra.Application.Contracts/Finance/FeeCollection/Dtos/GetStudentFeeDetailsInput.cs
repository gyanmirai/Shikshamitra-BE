using System;

namespace ShikshaMitra.Finance.FeeCollection;

public class GetStudentFeeDetailsInput
{
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
}
