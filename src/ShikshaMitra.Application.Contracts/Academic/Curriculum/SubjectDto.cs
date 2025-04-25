using ShikshaMitra.HR.Employees;
using System;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectDto
{
	public Guid Id { get; set; }
	public Guid ClassId { get; set; }
	public string? SubjectCode { get; set; }
	public string? SubjectName { get; set; }
	public Guid PrimaryTeacherId { get; set; }
	public EmployeeDto PrimaryTeacher { get; set; }
	public DateTimeOffset ScheduledDate { get; set; }
	public string? SubjectColor { get; set; }
	public string? LearningGoals { get; set; }
	public string? Prerequisites { get; set; }
	public string? Description { get; set; }
}
