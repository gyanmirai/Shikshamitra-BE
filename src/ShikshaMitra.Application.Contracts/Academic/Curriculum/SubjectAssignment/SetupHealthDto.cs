using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Curriculum;

public class SetupHealthDto
{
    public Guid AssignedSubjectId { get; set; }
    public string? SubjectName { get; set; }
    public int HealthScore { get; set; }
    public List<string> MissingItems { get; set; } = [];
}
