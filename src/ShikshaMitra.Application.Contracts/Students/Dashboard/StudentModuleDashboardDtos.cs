using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Dashboard;

public interface IStudentModuleDashboardAppService : IApplicationService
{
	Task<StudentModuleDashboardContextDto> GetContextAsync();
	Task<StudentModuleDashboardSectionDto> GetSectionAsync(string section, Guid academicYearId, Guid? boardId = null, Guid? mediumId = null, Guid? standardId = null, Guid? divisionId = null);
	Task<StudentModuleDashboardFiltersDto> GetFiltersAsync(Guid academicYearId);
	Task<Volo.Abp.Application.Dtos.PagedResultDto<StudentModuleDashboardRegisterDto>> GetAttendanceRegistersAsync(Guid academicYearId, Guid? boardId = null, Guid? mediumId = null, Guid? standardId = null, Guid? divisionId = null, int skipCount = 0, int maxResultCount = 20);
}

public class StudentModuleDashboardContextDto
{
	public string Mode { get; set; } = "common";
	public string DisplayName { get; set; } = "";
	public string ScopeLabel { get; set; } = "Your permitted student work";
	public List<string> Sections { get; set; } = [];
	public List<StudentModuleDashboardLinkDto> Links { get; set; } = [];
	public List<StudentModuleDashboardPersonalStudentDto> MyStudents { get; set; } = [];
}

public class StudentModuleDashboardPersonalStudentDto
{
	public Guid StudentId { get; set; }
	public string Name { get; set; } = "";
	public string Relationship { get; set; } = "";
}

public class StudentModuleDashboardSectionDto
{
	public string Key { get; set; } = "";
	public Guid AcademicYearId { get; set; }
	public DateTime GeneratedAt { get; set; }
	public string Description { get; set; } = "";
	public List<StudentModuleDashboardMetricDto> Metrics { get; set; } = [];
	public List<StudentModuleDashboardStrengthDto> ClassStrength { get; set; } = [];
	public int ClassStrengthTotalCount { get; set; }
	public List<StudentModuleDashboardMetricDto> WorkGroups { get; set; } = [];
	public List<StudentModuleDashboardMetricDto> OnboardingTasks { get; set; } = [];
	public List<StudentModuleDashboardAttentionDto> AttentionItems { get; set; } = [];
	public int AttentionTotalCount { get; set; }
	public StudentModuleDashboardAttendanceTodayDto? AttendanceToday { get; set; }
}

public class StudentModuleDashboardMetricDto
{
	public string Id { get; set; } = "";
	public string Label { get; set; } = "";
	public int Value { get; set; }
	public string Tone { get; set; } = "neutral";
	public string Href { get; set; } = "";
	public string Permission { get; set; } = "";
}

public class StudentModuleDashboardLinkDto
{
	public string Id { get; set; } = "";
	public string Label { get; set; } = "";
	public string Description { get; set; } = "";
	public string Href { get; set; } = "";
	public string Icon { get; set; } = "";
	public string Permission { get; set; } = "";
	public string VisibilityPermission { get; set; } = "";
}

public class StudentModuleDashboardStrengthDto
{
	public Guid? StandardId { get; set; }
	public string Label { get; set; } = "";
	public int Students { get; set; }
	public string Href { get; set; } = "";
	public List<StudentModuleDashboardDivisionStrengthDto> Divisions { get; set; } = [];
}

public class StudentModuleDashboardFiltersDto
{
	public List<ShikshaMitra.Students.Records.StudentDirectoryOptionDto> Boards { get; set; } = [];
	public List<ShikshaMitra.Students.Records.StudentDirectoryOptionDto> Mediums { get; set; } = [];
	public List<ShikshaMitra.Students.Records.StudentDirectoryOptionDto> Standards { get; set; } = [];
	public List<ShikshaMitra.Students.Records.StudentDirectoryOptionDto> Divisions { get; set; } = [];
}

public class StudentModuleDashboardDivisionStrengthDto
{
	public Guid? DivisionId { get; set; }
	public string Label { get; set; } = "";
	public int Students { get; set; }
	public string Href { get; set; } = "";
}

public class StudentModuleDashboardAttentionDto
{
	public Guid CaseId { get; set; }
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = "";
	public string ClassName { get; set; } = "";
	public string? DivisionName { get; set; }
	public string Blocker { get; set; } = "";
	public string? OwnerName { get; set; }
	public DateTime DueAt { get; set; }
	public string Href { get; set; } = "";
}

public class StudentModuleDashboardAttendanceTodayDto
{
	public DateTime SchoolDate { get; set; }
	public string TimeZone { get; set; } = "";
	public string Availability { get; set; } = "available";
	public bool Provisional { get; set; }
	public int? Expected { get; set; }
	public int? Missing { get; set; }
	public int? Draft { get; set; }
	public int? Submitted { get; set; }
	public int? Review { get; set; }
	public string Href { get; set; } = "";
}

public class StudentModuleDashboardRegisterDto
{
	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; }
	public string ClassName { get; set; } = "";
	public string DivisionName { get; set; } = "";
	public DateTime Date { get; set; }
	public string Status { get; set; } = "";
	public string Href { get; set; } = "";
}
