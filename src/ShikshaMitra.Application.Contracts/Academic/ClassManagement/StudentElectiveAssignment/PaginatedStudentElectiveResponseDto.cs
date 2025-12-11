using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

public class PaginatedStudentElectiveResponseDto
{
	public int TotalRecords { get; set; }
	public List<StudentElectiveRecordDto> Records { get; set; } = new();
}
