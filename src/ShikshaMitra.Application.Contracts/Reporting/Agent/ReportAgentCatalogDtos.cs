using System.Collections.Generic;

namespace ShikshaMitra.Reporting.Agent;

public class ReportAgentCatalogDto
{
	public List<ReportAgentModuleDto> Modules { get; set; } = [];
}

public class ReportAgentModuleDto
{
	public string Id { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public bool CanBuild { get; set; }
	public bool CanExport { get; set; }
	public string Availability { get; set; } = ReportAgentModuleAvailability.CatalogOnly;
}

public static class ReportAgentModuleAvailability
{
	public const string Available = "Available";
	public const string CatalogOnly = "CatalogOnly";
}
