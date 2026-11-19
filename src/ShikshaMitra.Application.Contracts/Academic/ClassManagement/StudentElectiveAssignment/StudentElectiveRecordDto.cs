using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

public class StudentElectiveRecordDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = string.Empty;
	public string RollNo { get; set; } = string.Empty;
	public Guid? SelectedSubjectId { get; set; }
	public bool IsLocked { get; set; }
	public string? SelectedSubjectName { get; set; }
	public string? StudentPreference { get; set; }
	public List<ElectiveSubjectOptionDto> SelectableSubjects { get; set; } = new();
}
