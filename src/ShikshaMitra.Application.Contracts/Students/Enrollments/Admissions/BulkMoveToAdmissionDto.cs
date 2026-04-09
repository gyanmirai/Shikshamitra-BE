using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.Enrollments.Admissions;

/// <summary>
/// DTO for bulk moving students to admission
/// </summary>
public class BulkMoveToAdmissionDto
{
	public List<Guid> StudentIds { get; set; }

	/// <summary>
	/// The academic year for which the students are being admitted
	/// </summary>
	public Guid? AcademicYearId { get; set; }
}

/// <summary>
/// Result DTO for bulk move operation
/// </summary>
public class BulkMoveResultDto
{
	public int TotalRequested { get; set; }
	public int SuccessCount { get; set; }
	public int FailureCount { get; set; }
	public List<StudentSummaryDto> SucceededStudents { get; set; }
	public List<FailedStudentDto> FailedStudents { get; set; }
}

/// <summary>
/// Summary DTO for student
/// </summary>
public class StudentSummaryDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; }
}

/// <summary>
/// DTO for failed student in bulk operation
/// </summary>
public class FailedStudentDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; }
	public string Reason { get; set; }
}
