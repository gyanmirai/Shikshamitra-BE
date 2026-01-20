using System;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectLiteDto
{
    public SubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; }
    public string SubjectCode { get; set; }
    public string Color { get; set; }
}
