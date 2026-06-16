using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.Records;

public class UploadStudentPhotoDto
{
	public required string FileName { get; set; }
	public required string ContentType { get; set; }
	public required byte[] Content { get; set; }
}

public class StudentPhotoUploadResultDto
{
	public Guid StudentId { get; set; }
	public bool HasPhoto { get; set; }
	public string? PhotoUrl { get; set; }
}

public class StudentPhotoDownloadDto
{
	public required string FileName { get; set; }
	public required string ContentType { get; set; }
	public required byte[] Content { get; set; }
}

public class StudentDashboardSummaryDto
{
	public int TotalStudents { get; set; }
	public int MaleStudents { get; set; }
	public int FemaleStudents { get; set; }
	public List<StudentDashboardActivityDto> RecentActivities { get; set; } = [];
	public List<StudentBirthdayDto> TodaysBirthdays { get; set; } = [];
	public List<StudentBirthdayDto> UpcomingBirthdays { get; set; } = [];
}

public class StudentDashboardActivityDto
{
	public string Title { get; set; } = string.Empty;
	public string Subtitle { get; set; } = string.Empty;
	public string Tag { get; set; } = string.Empty;
	public string Date { get; set; } = string.Empty;
	public string TimeAgo { get; set; } = string.Empty;
}

public class StudentBirthdayDto
{
	public string Name { get; set; } = string.Empty;
	public int Age { get; set; }
	public string ClassName { get; set; } = string.Empty;
	public string? DateLabel { get; set; }
}

public class StudentAttendanceDashboardSummaryDto
{
	public decimal SchoolAttendancePercentage { get; set; }
	public List<StudentAttendanceClassSummaryDto> Classes { get; set; } = [];
}

public class GetStudentAttendanceDashboardSummaryInput
{
	public DateTime? StartDate { get; set; }
	public DateTime? EndDate { get; set; }
}

public class StudentAttendanceClassSummaryDto
{
	public Guid? StandardId { get; set; }
	public string Name { get; set; } = string.Empty;
	public decimal Percentage { get; set; }
	public List<StudentAttendanceDivisionSummaryDto> Divisions { get; set; } = [];
}

public class StudentAttendanceDivisionSummaryDto
{
	public Guid? DivisionId { get; set; }
	public string Name { get; set; } = string.Empty;
	public decimal Percentage { get; set; }
}
