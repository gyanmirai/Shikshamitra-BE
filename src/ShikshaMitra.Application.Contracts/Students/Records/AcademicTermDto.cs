using System;

namespace ShikshaMitra.Students.Records;

public class AcademicTermDto
{
	public Guid Id { get; set; }
	public Guid AcademicYearId { get; set; }
	public string Name { get; set; } = string.Empty;
	public int Sequence { get; set; }
	public bool IsCurrent { get; set; }
	public bool IsActive { get; set; }
}

public class CreateAcademicTermDto
{
	public Guid AcademicYearId { get; set; }
	public string Name { get; set; } = string.Empty;
	public int Sequence { get; set; }
	public bool IsCurrent { get; set; }
}
