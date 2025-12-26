using System;

namespace ShikshaMitra.Students.Records;

public class UpdateStudentRollNumberDto
{
    public Guid StudentId { get; set; }
    public int? RollNo { get; set; }
}
