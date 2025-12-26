using System;

namespace ShikshaMitra.Students.Records;

public class UpdateStudentGrNumberDto
{
    public Guid StudentId { get; set; }
    public int? GrNo { get; set; }
}
