using System;

namespace ShikshaMitra.Students.Records;

public class StudentLookupDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = string.Empty;
	public int? GrNo { get; set; }
	public int? RollNo { get; set; }
	public Guid? StandardId { get; set; }
	public string StandardName { get; set; } = string.Empty;
	public Guid? DivisionId { get; set; }
	public string DivisionName { get; set; } = string.Empty;
	public GenderEnum? Gender { get; set; }
	public bool HasFeeStructureAssignment { get; set; }
	public bool HasChargeInCategory { get; set; }
}
