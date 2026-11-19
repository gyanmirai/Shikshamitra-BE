using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.Records;

public sealed class StudentSchoolExitSummaryDto
{
	public Guid StudentId { get; set; }
	public string? StudentName { get; set; }
	public Guid AcademicYearId { get; set; }
	public string StudentConcurrencyStamp { get; set; } = "";
	public string EnrollmentConcurrencyStamp { get; set; } = "";
	public bool CanSubmit { get; set; }
	public StudentSchoolExitSectionDto Fees { get; set; } = new();
	public StudentSchoolExitSectionDto Library { get; set; } = new();
	public StudentSchoolExitSectionDto Documents { get; set; } = new();
	public List<StudentRecordTransitionDto> PendingTransitions { get; set; } = [];
}

public sealed class StudentSchoolExitSectionDto
{
	public string Status { get; set; } = "Unavailable";
	public string Detail { get; set; } = "";
	public decimal? OutstandingAmount { get; set; }
	public int? PendingCount { get; set; }
	public List<StudentSchoolExitDocumentDto> Items { get; set; } = [];
}

public sealed class StudentSchoolExitDocumentDto
{
	public string Name { get; set; } = "";
	public string Status { get; set; } = "";
}
