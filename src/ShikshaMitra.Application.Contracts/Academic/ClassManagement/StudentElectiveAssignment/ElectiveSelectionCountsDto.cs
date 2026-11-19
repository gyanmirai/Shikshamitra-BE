using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

public class ElectiveSelectionCountsRequestDto
{
    public Guid AcademicMediumId { get; set; }
    public Guid ClassId { get; set; }
    public List<Guid> DivisionIds { get; set; } = [];
    public List<Guid> ElectiveGroupIds { get; set; } = [];
}

public class ElectiveSelectionCountDto
{
    public Guid ElectiveGroupId { get; set; }
    public Guid DivisionId { get; set; }
    public Guid SubjectId { get; set; }
    public int Count { get; set; }
}
