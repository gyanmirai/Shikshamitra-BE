using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.FeeCollection;

public class StudentFeeCollectionListDto
{
    public List<StudentFeeCollectionItemDto> Items { get; set; } = [];
    public long TotalCount { get; set; }
}

public class StudentFeeCollectionItemDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string GrNumber { get; set; } = string.Empty;
    public double TotalDue { get; set; }
    public double TotalPaid { get; set; }
    public FeeCollectionStatus Status { get; set; }
}
