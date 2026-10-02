using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.Records;

public class StudentRecordPlacementPayload
{
	public Guid AcademicYearId { get; set; }
	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; }
	[Range(1, int.MaxValue)] public int? RollNumber { get; set; }
}
public class StudentRecordStatusPayload
{
	public StudentLifeCycleStatusEnum Status { get; set; }
}
