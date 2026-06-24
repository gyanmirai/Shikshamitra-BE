using System.Collections.Generic;

namespace ShikshaMitra.Academic.ClassManagement.Dashboard;

public class ClassManagementDashboardDto
{
	public string Role { get; set; } = "Admin";
	public int HealthScore { get; set; }
	public string HealthLabel { get; set; } = string.Empty;
	public List<ClassManagementDashboardMetricDto> Metrics { get; set; } = [];
	public List<ClassManagementDashboardAlertDto> Alerts { get; set; } = [];
	public List<ClassManagementDashboardActionDto> Actions { get; set; } = [];
	public List<ClassManagementDashboardProgressDto> ProgressItems { get; set; } = [];
	public List<ClassManagementDashboardClassRiskDto> ClassRisks { get; set; } = [];
	public List<ClassManagementDashboardWorkloadDto> TeacherWorkload { get; set; } = [];
	public ClassManagementDashboardTrendDto ProgressTrend { get; set; } = new();
}

public class ClassManagementDashboardMetricDto
{
	public string Id { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public string Value { get; set; } = string.Empty;
	public string Helper { get; set; } = string.Empty;
	public string Severity { get; set; } = "info";
}

public class ClassManagementDashboardAlertDto
{
	public string Id { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Severity { get; set; } = "info";
	public string Category { get; set; } = string.Empty;
	public string ActionLabel { get; set; } = string.Empty;
	public string? ActionHref { get; set; }
}

public class ClassManagementDashboardActionDto
{
	public string Id { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public int Count { get; set; }
	public string Priority { get; set; } = "medium";
	public string ActionLabel { get; set; } = string.Empty;
	public string ActionHref { get; set; } = string.Empty;
}

public class ClassManagementDashboardProgressDto
{
	public string Id { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public string SubLabel { get; set; } = string.Empty;
	public int Value { get; set; }
	public int Target { get; set; }
	public string Status { get; set; } = "on-track";
}

public class ClassManagementDashboardClassRiskDto
{
	public string Id { get; set; } = string.Empty;
	public string ClassName { get; set; } = string.Empty;
	public string DivisionName { get; set; } = string.Empty;
	public int Students { get; set; }
	public int Capacity { get; set; }
	public int CourseProgress { get; set; }
	public string ClassTeacher { get; set; } = string.Empty;
	public string RiskLevel { get; set; } = "low";
	public string RiskReason { get; set; } = string.Empty;
}

public class ClassManagementDashboardWorkloadDto
{
	public string Id { get; set; } = string.Empty;
	public string TeacherName { get; set; } = string.Empty;
	public int Classes { get; set; }
	public int Subjects { get; set; }
	public int LoadScore { get; set; }
	public string Status { get; set; } = "balanced";
}

public class ClassManagementDashboardTrendDto
{
	public List<string> Labels { get; set; } = [];
	public List<int> Planned { get; set; } = [];
	public List<int> Actual { get; set; } = [];
}
