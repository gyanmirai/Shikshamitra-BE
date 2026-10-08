using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Dashboard;

public interface IStudentModuleDashboardAppService : IApplicationService
{
	Task<StudentModuleDashboardContextDto> GetContextAsync();
	Task<StudentModuleDashboardSectionDto> GetSectionAsync(string section, Guid academicYearId);
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
}
