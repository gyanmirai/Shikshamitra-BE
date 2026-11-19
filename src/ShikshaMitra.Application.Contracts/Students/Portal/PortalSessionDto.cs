using System.Collections.Generic;
using ShikshaMitra.Academic.Timetable.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;

namespace ShikshaMitra.Students.Portal;

public class PortalSessionDto
{
	public int Version { get; set; } = 1;
	public string Status { get; set; } = "restricted";
	public List<PortalSessionStudentDto> Students { get; set; } = [];
}

public class PortalSessionStudentDto
{
	public StudentPortalAccountDto Account { get; set; } = new();
	public PortalTimetableContextDto? Context { get; set; }
	public string ContextStatus { get; set; } = "unavailable";
}
