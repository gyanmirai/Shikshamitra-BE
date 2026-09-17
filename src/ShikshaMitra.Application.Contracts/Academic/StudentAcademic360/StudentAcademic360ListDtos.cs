using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.StudentAcademic360;

public enum StudentAcademic360SmartFilter
{
	All = 0,
	NeedsAttention = 1,
	LowAttendance = 2,
	OverdueHomework = 3,
	PerformanceDecline = 4,
	NoData = 5
}

public class GetStudentAcademic360ListInput : PagedAndSortedResultRequestDto
{
	public Guid AcademicYearId { get; set; }
	public Guid BoardId { get; set; }
	public Guid EducationMediumId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
	public string? SearchTerm { get; set; }
	public StudentAcademic360SmartFilter SmartFilter { get; set; }
}

public class StudentAcademic360ListResultDto
{
	public long TotalCount { get; set; }
	public List<StudentAcademic360ListItemDto> Items { get; set; } = [];
	public StudentAcademic360ListSummaryDto Summary { get; set; } = new();
}

public class StudentAcademic360ListItemDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = string.Empty;
	public string? PhotoUrl { get; set; }
	public int? GrNo { get; set; }
	public int? RollNo { get; set; }
	public Guid? StandardId { get; set; }
	public string StandardName { get; set; } = string.Empty;
	public Guid? DivisionId { get; set; }
	public string DivisionName { get; set; } = string.Empty;
	public StudentAcademic360ListMetricDto? Attendance { get; set; }
	public StudentAcademic360ListMetricDto? Homework { get; set; }
	public StudentAcademic360ListMetricDto? ExamAverage { get; set; }
	public string Trend { get; set; } = "Insufficient Data";
	public string AcademicHealth { get; set; } = "Insufficient Data";
	public List<string> AcademicHealthReasons { get; set; } = [];
	public int AlertCount { get; set; }
	public string DataCompleteness { get; set; } = "Insufficient Data";
	public DateTime? LastUpdatedAt { get; set; }
}

public class StudentAcademic360ListMetricDto
{
	public double? Percentage { get; set; }
	public int? Completed { get; set; }
	public int? Total { get; set; }
	public int? Pending { get; set; }
	public int? Count { get; set; }
	public string? Status { get; set; }
}

public class StudentAcademic360ListSummaryDto
{
	public int TotalStudents { get; set; }
	public int NeedsAttention { get; set; }
	public double? AverageAttendance { get; set; }
	public double? HomeworkCompletion { get; set; }
	public double? AverageExamScore { get; set; }
}
